# Plan de Implementacion: PostgreSQL Database Project Extension para Visual Studio

## 1. Vision General

Extension de Visual Studio (VSIX) que define un nuevo tipo de proyecto **"PostgreSQL Database Project"** (`.pgproj`), inspirado en el SQL Server Database Project (SSDT). Permite gestionar scripts DDL organizados por tipo de objeto y ofrece una funcion de **Import** desde una base de datos PostgreSQL existente usando Npgsql.

---

## 2. Arquitectura de la Solucion

```
PgDatabaseProject.sln
|
+-- src/
|   +-- PgDatabaseProject.Core/                 # Modelos compartidos, enums, utilidades
|   +-- PgDatabaseProject.SchemaExtractor/       # Logica de extraccion de DDL via Npgsql
|   +-- PgDatabaseProject.ProjectSystem/         # Tipo de proyecto CPS (.pgproj), build targets
|   +-- PgDatabaseProject.Extension/             # VSPackage, comandos, menus, wizard de importacion
|   +-- PgDatabaseProject.Vsix/                  # Empaquetado VSIX final
|
+-- templates/
|   +-- PostgreSQLDatabaseProject/               # Template del proyecto (.vstemplate + .pgproj)
|
+-- tests/
|   +-- PgDatabaseProject.Core.Tests/
|   +-- PgDatabaseProject.SchemaExtractor.Tests/
|
+-- docs/
    +-- user-guide.md
```

---

## 3. Estructura del Proyecto Generado (.pgproj)

Cuando el usuario crea o importa un PostgreSQL Database Project, el resultado es:

```
MiBaseDeDatos/
+-- MiBaseDeDatos.pgproj                # Archivo de proyecto MSBuild
+-- Schemas/
|   +-- public.sql
|   +-- app.sql
+-- Tables/
|   +-- public/
|   |   +-- users.sql
|   |   +-- orders.sql
|   +-- app/
|       +-- config.sql
+-- Views/
|   +-- public/
|       +-- active_users.sql
+-- Functions/
|   +-- public/
|       +-- calculate_total.sql
+-- StoredProcedures/
|   +-- public/
|       +-- process_order.sql
+-- Sequences/
|   +-- public/
|       +-- users_id_seq.sql
+-- Types/
|   +-- public/
|       +-- address_type.sql
+-- Triggers/
|   +-- public/
|       +-- orders_audit_trigger.sql
+-- Extensions/
|   +-- postgis.sql
|   +-- uuid-ossp.sql
+-- Indexes/
|   +-- public/
|       +-- idx_users_email.sql
```

### 3.1 Formato del archivo .pgproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ProjectTypeGuids>{GUID-PG-PROJECT}</ProjectTypeGuids>
    <TargetFramework>net8.0</TargetFramework>
    <PgDefaultSchema>public</PgDefaultSchema>
    <PgTargetVersion>16</PgTargetVersion>
  </PropertyGroup>

  <ItemGroup>
    <SqlScript Include="Tables\**\*.sql" ObjectType="Table" />
    <SqlScript Include="Views\**\*.sql" ObjectType="View" />
    <SqlScript Include="Functions\**\*.sql" ObjectType="Function" />
    <SqlScript Include="StoredProcedures\**\*.sql" ObjectType="StoredProcedure" />
    <SqlScript Include="Sequences\**\*.sql" ObjectType="Sequence" />
    <SqlScript Include="Types\**\*.sql" ObjectType="Type" />
    <SqlScript Include="Triggers\**\*.sql" ObjectType="Trigger" />
    <SqlScript Include="Schemas\**\*.sql" ObjectType="Schema" />
    <SqlScript Include="Extensions\**\*.sql" ObjectType="Extension" />
    <SqlScript Include="Indexes\**\*.sql" ObjectType="Index" />
  </ItemGroup>
</Project>
```

---

## 4. Componentes Detallados

### 4.1 PgDatabaseProject.Core

**Responsabilidad:** Modelos y contratos compartidos entre todos los proyectos.

| Archivo | Descripcion |
|---------|-------------|
| `Models/DatabaseObjectType.cs` | Enum: Table, View, Function, StoredProcedure, Sequence, Type, Trigger, Schema, Extension, Index |
| `Models/DatabaseObject.cs` | Record: Name, Schema, ObjectType, DdlScript, DependsOn[] |
| `Models/ConnectionSettings.cs` | Record: Host, Port, Database, Username, Password, SslMode |
| `Models/ImportOptions.cs` | Record: Schemas[], ObjectTypes[], IncludeSystemObjects, ScriptPerObject |
| `Interfaces/ISchemaExtractor.cs` | Contrato para la extraccion de esquema |
| `Interfaces/IScriptWriter.cs` | Contrato para escribir scripts a disco |

**Target framework:** `netstandard2.0` (compartible con VS y tests).

### 4.2 PgDatabaseProject.SchemaExtractor

**Responsabilidad:** Conectar a PostgreSQL via Npgsql y extraer DDLs de todos los objetos.

**Dependencias:** `Npgsql` (>= 8.0), `PgDatabaseProject.Core`

#### Clases principales:

| Clase | Descripcion |
|-------|-------------|
| `PgSchemaExtractor` | Implementa `ISchemaExtractor`. Orquesta la extraccion completa. |
| `Extractors/TableExtractor.cs` | Extrae CREATE TABLE con columnas, constraints, defaults, NOT NULL |
| `Extractors/ViewExtractor.cs` | Usa `pg_get_viewdef()` |
| `Extractors/FunctionExtractor.cs` | Usa `pg_get_functiondef()` para funciones y procedures |
| `Extractors/SequenceExtractor.cs` | Extrae CREATE SEQUENCE con parametros |
| `Extractors/TypeExtractor.cs` | Enum types, composite types, domains |
| `Extractors/TriggerExtractor.cs` | Extrae CREATE TRIGGER + funcion asociada |
| `Extractors/SchemaExtractor.cs` | Extrae CREATE SCHEMA |
| `Extractors/ExtensionExtractor.cs` | Extrae CREATE EXTENSION |
| `Extractors/IndexExtractor.cs` | Usa `pg_get_indexdef()` |
| `ScriptFormatter.cs` | Formatea DDL con header, comentarios, terminadores |
| `ScriptFileWriter.cs` | Implementa `IScriptWriter`, escribe archivos .sql a disco en la estructura de carpetas |

#### Consultas clave de extraccion:

**Tablas** (reconstruccion desde catalogo):
```sql
-- Listar tablas
SELECT schemaname, tablename
FROM pg_tables
WHERE schemaname NOT IN ('pg_catalog', 'information_schema');

-- Columnas de una tabla
SELECT column_name, data_type, column_default, is_nullable,
       character_maximum_length, numeric_precision, numeric_scale
FROM information_schema.columns
WHERE table_schema = @schema AND table_name = @table
ORDER BY ordinal_position;

-- Constraints (PK, FK, UNIQUE, CHECK)
SELECT conname, contype, pg_get_constraintdef(oid, true)
FROM pg_constraint
WHERE conrelid = '@schema.@table'::regclass;
```

**Funciones y Procedures:**
```sql
SELECT n.nspname, p.proname, pg_get_functiondef(p.oid) AS definition
FROM pg_proc p
JOIN pg_namespace n ON p.pronamespace = n.oid
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND p.prokind IN ('f', 'p');  -- f=function, p=procedure
```

**Vistas:**
```sql
SELECT schemaname, viewname, pg_get_viewdef(c.oid, true) AS definition
FROM pg_views v
JOIN pg_class c ON c.relname = v.viewname
JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = v.schemaname
WHERE schemaname NOT IN ('pg_catalog', 'information_schema');
```

**Secuencias:**
```sql
SELECT sequence_schema, sequence_name, data_type,
       start_value, minimum_value, maximum_value, increment
FROM information_schema.sequences
WHERE sequence_schema NOT IN ('pg_catalog', 'information_schema');
```

**Triggers:**
```sql
SELECT trigger_schema, trigger_name, event_object_table,
       action_timing, event_manipulation, action_statement
FROM information_schema.triggers
WHERE trigger_schema NOT IN ('pg_catalog', 'information_schema');
```

**Types (enums, composites, domains):**
```sql
-- Enums
SELECT n.nspname, t.typname,
       array_agg(e.enumlabel ORDER BY e.enumsortorder) AS labels
FROM pg_type t
JOIN pg_namespace n ON t.typnamespace = n.oid
JOIN pg_enum e ON e.enumtypid = t.oid
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
GROUP BY n.nspname, t.typname;

-- Composite types
SELECT n.nspname, t.typname
FROM pg_type t
JOIN pg_namespace n ON t.typnamespace = n.oid
WHERE t.typtype = 'c'
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.reltype = t.oid AND c.relkind != 'c');

-- Domains
SELECT n.nspname, t.typname, pg_catalog.format_type(t.typbasetype, t.typtypmod) AS base_type,
       t.typnotnull, t.typdefault
FROM pg_type t
JOIN pg_namespace n ON t.typnamespace = n.oid
WHERE t.typtype = 'd'
  AND n.nspname NOT IN ('pg_catalog', 'information_schema');
```

**Indexes (standalone, no PK/UNIQUE):**
```sql
SELECT schemaname, indexname, indexdef
FROM pg_indexes
WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
  AND indexname NOT IN (
    SELECT conname FROM pg_constraint
    WHERE contype IN ('p', 'u')
  );
```

**Extensions:**
```sql
SELECT extname, extversion FROM pg_extension WHERE extname != 'plpgsql';
```

**Schemas:**
```sql
SELECT nspname FROM pg_namespace
WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
  AND nspname NOT LIKE 'pg_temp_%';
```

### 4.3 PgDatabaseProject.ProjectSystem

**Responsabilidad:** Definir el tipo de proyecto `.pgproj` usando CPS (Common Project System) para que VS lo reconozca.

**Dependencias:** `Microsoft.VisualStudio.ProjectSystem` NuGet packages.

| Archivo | Descripcion |
|-------|-------------|
| `PgProjectCapabilities.cs` | Define capabilities del proyecto (no-compile, folder structure) |
| `PgUnconfiguredProject.cs` | Punto de entrada CPS para el proyecto no configurado |
| `BuildSystem/PgDatabaseProject.targets` | MSBuild targets custom (validacion SQL basica) |
| `BuildSystem/PgDatabaseProject.props` | Propiedades por defecto del proyecto |
| `Rules/PgProjectProperties.xaml` | Propiedades del proyecto visibles en VS (esquema default, version PG, connection) |
| `Rules/SqlScript.xaml` | Propiedades por item .sql (ObjectType, Schema) |

#### CPS Capabilities:

```csharp
[Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectCapabilitiesProvider))]
[AppliesTo("PostgreSQLDatabaseProject")]
internal class PgProjectCapabilitiesProvider : IProjectCapabilitiesProvider
{
    public Task<IReadOnlyCollection<string>> GetCapabilitiesAsync()
    {
        return Task.FromResult<IReadOnlyCollection<string>>(
            new[] { "PostgreSQLDatabaseProject", "NoCompile" });
    }
}
```

### 4.4 PgDatabaseProject.Extension

**Responsabilidad:** VSPackage que registra comandos, menus y el wizard de importacion en Visual Studio.

**Dependencias:** `Microsoft.VisualStudio.Sdk` (17.x), `Microsoft.VisualStudio.Shell.*`, `PgDatabaseProject.SchemaExtractor`, `PgDatabaseProject.Core`

| Archivo | Descripcion |
|-------|-------------|
| `PgDatabasePackage.cs` | VSPackage principal, registra servicios y comandos |
| `Commands/ImportSchemaCommand.cs` | Comando "Import Database..." en menu contextual del proyecto |
| `Commands/RefreshImportCommand.cs` | Comando "Refresh from Database..." (re-import selectivo) |
| `Dialogs/ImportWizardDialog.xaml` | Ventana WPF - wizard de 3 pasos |
| `Dialogs/ConnectionPage.xaml` | Paso 1: Configuracion de conexion PostgreSQL |
| `Dialogs/ObjectSelectionPage.xaml` | Paso 2: Seleccion de esquemas y tipos de objetos |
| `Dialogs/ImportProgressPage.xaml` | Paso 3: Progreso de importacion con log |
| `Dialogs/ViewModels/ImportWizardViewModel.cs` | ViewModel principal del wizard |
| `Dialogs/ViewModels/ConnectionPageViewModel.cs` | ViewModel con Test Connection |
| `Dialogs/ViewModels/ObjectSelectionPageViewModel.cs` | ViewModel con tree de objetos seleccionables |
| `Dialogs/ViewModels/ImportProgressViewModel.cs` | ViewModel con progreso y cancelacion |

#### Flujo del Import Wizard:

```
[Usuario hace clic derecho en proyecto] -> "Import Database..."
    |
    v
+--[ Paso 1: Conexion ]--------------------------------+
|  Host: [localhost     ]  Port: [5432]                 |
|  Database: [mydb      ]                               |
|  Username: [postgres  ]  Password: [****]             |
|  SSL Mode: [Prefer   v]                               |
|  [Test Connection]  -> "Connection successful!"       |
|                                        [Next >]       |
+-------------------------------------------------------+
    |
    v
+--[ Paso 2: Seleccion de Objetos ]---------------------+
|  Schemas:                                              |
|    [x] public                                          |
|    [x] app                                             |
|    [ ] pg_catalog                                      |
|                                                        |
|  Object Types:                                         |
|    [x] Tables (23)     [x] Views (5)                   |
|    [x] Functions (12)  [x] Stored Procedures (3)       |
|    [x] Sequences (8)   [x] Types (2)                   |
|    [x] Triggers (4)    [x] Indexes (15)                |
|    [x] Extensions (2)                                  |
|                                                        |
|  Options:                                              |
|    [x] One file per object                             |
|    [ ] Include DROP IF EXISTS                          |
|    [x] Overwrite existing files                        |
|                              [< Back] [Import >]       |
+--------------------------------------------------------+
    |
    v
+--[ Paso 3: Progreso ]---------------------------------+
|  Importing objects...                                  |
|  [====================              ] 65%              |
|                                                        |
|  > Extracting Tables... (15/23)                        |
|    public.users                OK                      |
|    public.orders               OK                      |
|    public.products             OK                      |
|    ...                                                 |
|                                                        |
|  [Cancel]                           [Close]            |
+--------------------------------------------------------+
```

#### Registro del comando en el menu contextual del proyecto:

```xml
<!-- PgDatabasePackage.vsct -->
<Commands package="guidPgDatabasePackage">
  <Buttons>
    <Button guid="guidPgCmdSet" id="cmdImportSchema" priority="0x0100" type="Button">
      <Parent guid="guidSHLMainMenu" id="IDG_VS_CTXT_PROJECT_ADD" />
      <CommandFlag>DynamicVisibility</CommandFlag>
      <Strings>
        <ButtonText>Import PostgreSQL Database...</ButtonText>
      </Strings>
    </Button>
  </Buttons>
</Commands>
```

El comando solo sera visible cuando el proyecto activo sea de tipo `.pgproj`.

### 4.5 PgDatabaseProject.Vsix

**Responsabilidad:** Proyecto de empaquetado VSIX que agrupa todo para distribucion.

| Archivo | Descripcion |
|-------|-------------|
| `source.extension.vsixmanifest` | Metadatos de la extension (nombre, version, autor, targets) |

Assets incluidos:
- `Microsoft.VisualStudio.ProjectTemplate` -> template del proyecto
- `Microsoft.VisualStudio.VsPackage` -> el VSPackage
- `Microsoft.VisualStudio.MefComponent` -> componentes CPS

### 4.6 Templates

**`templates/PostgreSQLDatabaseProject/`**

| Archivo | Descripcion |
|-------|-------------|
| `PostgreSQLDatabaseProject.vstemplate` | Definicion del template |
| `Project.pgproj` | .pgproj template con estructura base |
| `Tables/.gitkeep` | Carpeta placeholder |
| `Views/.gitkeep` | Carpeta placeholder |
| `Functions/.gitkeep` | Carpeta placeholder |
| `StoredProcedures/.gitkeep` | Carpeta placeholder |
| `Sequences/.gitkeep` | Carpeta placeholder |
| `Types/.gitkeep` | Carpeta placeholder |
| `Triggers/.gitkeep` | Carpeta placeholder |
| `Schemas/.gitkeep` | Carpeta placeholder |
| `Extensions/.gitkeep` | Carpeta placeholder |
| `Indexes/.gitkeep` | Carpeta placeholder |

```xml
<!-- PostgreSQLDatabaseProject.vstemplate -->
<VSTemplate Version="3.0.0" Type="Project"
    xmlns="http://schemas.microsoft.com/developer/vstemplate/2005">
  <TemplateData>
    <Name>PostgreSQL Database Project</Name>
    <Description>A project for managing PostgreSQL database schemas with SQL scripts organized by object type.</Description>
    <Icon>pg-icon.ico</Icon>
    <ProjectType>CSharp</ProjectType>
    <DefaultName>PostgreSQLDatabase</DefaultName>
    <CreateNewFolder>true</CreateNewFolder>
    <ProvideDefaultName>true</ProvideDefaultName>
  </TemplateData>
  <TemplateContent>
    <Project File="Project.pgproj" ReplaceParameters="true">
      <Folder Name="Schemas" />
      <Folder Name="Tables" />
      <Folder Name="Views" />
      <Folder Name="Functions" />
      <Folder Name="StoredProcedures" />
      <Folder Name="Sequences" />
      <Folder Name="Types" />
      <Folder Name="Triggers" />
      <Folder Name="Extensions" />
      <Folder Name="Indexes" />
    </Project>
  </TemplateContent>
</VSTemplate>
```

---

## 5. Fases de Implementacion

### Fase 1: Fundacion (Core + SchemaExtractor)
**Objetivo:** Poder conectar a PostgreSQL y extraer DDLs completos como archivos .sql.

1. Crear solution `PgDatabaseProject.sln`
2. Implementar `PgDatabaseProject.Core` con modelos e interfaces
3. Implementar `PgDatabaseProject.SchemaExtractor`:
   - Conexion via Npgsql
   - Extractors individuales para cada tipo de objeto
   - `ScriptFileWriter` que genera la estructura de carpetas
4. Implementar `PgDatabaseProject.SchemaExtractor.Tests`:
   - Tests unitarios con mocks para las queries
   - Tests de integracion con Testcontainers (PostgreSQL en Docker)

**Entregable:** Libreria de consola que extrae DDLs de una DB PostgreSQL a disco.

### Fase 2: Tipo de Proyecto Visual Studio (.pgproj)
**Objetivo:** VS reconoce `.pgproj` como tipo de proyecto valido con su estructura de carpetas.

1. Implementar `PgDatabaseProject.ProjectSystem`:
   - Registrar project type con CPS
   - Definir capabilities (NoCompile, folder-based)
   - Crear rules XAML para propiedades del proyecto y de items
   - Crear MSBuild .targets y .props
2. Crear template de proyecto en `templates/`
3. Configurar `PgDatabaseProject.Vsix` con manifest

**Entregable:** Se puede crear un "PostgreSQL Database Project" desde File > New > Project en VS.

### Fase 3: Extension y Comando Import
**Objetivo:** Desde VS, el usuario puede importar esquema de una DB PostgreSQL.

1. Implementar `PgDatabaseProject.Extension`:
   - `PgDatabasePackage` con registro de comandos
   - Comando "Import PostgreSQL Database..." en menu contextual
   - Dialogo WPF con los 3 pasos del wizard
   - ViewModels con logica de conexion, seleccion e importacion
2. Integrar `SchemaExtractor` con el wizard
3. Los archivos importados se agregan automaticamente al .pgproj

**Entregable:** Flujo completo de importacion funcional desde VS.

### Fase 4: Pulido y Features Adicionales
**Objetivo:** Mejorar la experiencia de uso.

1. **Refresh selectivo:** Re-importar solo objetos seleccionados sin perder cambios manuales
2. **Diff de importacion:** Mostrar diferencias antes de sobrescribir archivos existentes
3. **Syntax highlighting:** Registrar `.sql` dentro del contexto de PostgreSQL para mejor coloreo
4. **Validacion basica en build:** MSBuild target que valide sintaxis SQL basica al compilar
5. **Guardar conexion:** Persistir settings de conexion en un archivo `.pgproj.user` (excluido de source control)
6. **Iconos personalizados:** Iconos diferentes en Solution Explorer por tipo de objeto

---

## 6. Dependencias NuGet Principales

| Paquete | Proyecto | Version |
|---------|----------|---------|
| `Npgsql` | SchemaExtractor | >= 8.0 |
| `Microsoft.VisualStudio.Sdk` | Extension | 17.x |
| `Microsoft.VSSDK.BuildTools` | Vsix | 17.x |
| `Microsoft.VisualStudio.ProjectSystem` | ProjectSystem | 17.x |
| `Microsoft.VisualStudio.Shell.15.0` | Extension | 17.x |
| `Microsoft.VisualStudio.Threading` | Extension | 17.x |
| `CommunityToolkit.Mvvm` | Extension (ViewModels) | >= 8.0 |
| `xunit` | Tests | >= 2.9 |
| `Testcontainers.PostgreSql` | Tests (integracion) | >= 4.0 |

---

## 7. Decisiones Tecnicas Clave

| Decision | Eleccion | Razon |
|----------|----------|-------|
| Modelo de extensibilidad | VSSDK + CPS (in-process) | CPS necesario para custom project types; VisualStudio.Extensibility aun no soporta project types |
| Formato de proyecto | MSBuild SDK-style (.pgproj) | Familiar, extensible, compatible con CI/CD |
| Extraccion de DDL | Queries a `pg_catalog` + funciones `pg_get_*def()` | Es la forma nativa y mas precisa de obtener DDL de PostgreSQL |
| UI del wizard | WPF (MVVM) | Standard para dialogos en extensiones VSSDK |
| Organizacion de scripts | Un archivo .sql por objeto, agrupados en carpetas por tipo y sub-carpetas por schema | Maximo control en source control, facil de navegar |
| Target framework del VSIX | net472 (para el VSPackage) + netstandard2.0 (Core/SchemaExtractor) | Requisito de VS para extensiones in-process |

---

## 8. Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigacion |
|--------|---------|------------|
| CPS tiene poca documentacion publica | Retraso en Fase 2 | Estudiar el codigo fuente de CPS en GitHub, y proyectos existentes como Microsoft/VSProjectSystem |
| DDL de tablas no tiene `pg_get_tabledef()` nativo | Complejidad en TableExtractor | Reconstruir CREATE TABLE pieza por pieza desde catalogo; usar libreria auxiliar si existe |
| Compatibilidad con multiples versiones de PG (12-17) | Queries pueden variar | Detectar version con `SHOW server_version` y adaptar queries |
| Tamano de la DB a importar (miles de objetos) | Performance y UX | Importacion asincrona con progreso, cancelable, batch de escritura |
| Credenciales de conexion | Seguridad | Nunca persistir password en .pgproj; usar .pgproj.user (gitignored) o SecureString en memoria |

---

## 9. Estructura de Archivos Inicial a Crear

```
D:\dev\PostgresDatabaseProject\
+-- PgDatabaseProject.sln
+-- Directory.Build.props                    # Propiedades comunes (version, nullable, etc.)
+-- Directory.Packages.props                 # Central Package Management
+-- .gitignore
+-- .editorconfig
+-- src/
|   +-- PgDatabaseProject.Core/
|   |   +-- PgDatabaseProject.Core.csproj
|   |   +-- Models/
|   |   +-- Interfaces/
|   +-- PgDatabaseProject.SchemaExtractor/
|   |   +-- PgDatabaseProject.SchemaExtractor.csproj
|   |   +-- PgSchemaExtractor.cs
|   |   +-- Extractors/
|   |   +-- ScriptFormatter.cs
|   |   +-- ScriptFileWriter.cs
|   +-- PgDatabaseProject.ProjectSystem/
|   |   +-- PgDatabaseProject.ProjectSystem.csproj
|   |   +-- BuildSystem/
|   |   +-- Rules/
|   +-- PgDatabaseProject.Extension/
|   |   +-- PgDatabaseProject.Extension.csproj
|   |   +-- PgDatabasePackage.cs
|   |   +-- Commands/
|   |   +-- Dialogs/
|   +-- PgDatabaseProject.Vsix/
|       +-- PgDatabaseProject.Vsix.csproj
|       +-- source.extension.vsixmanifest
+-- templates/
|   +-- PostgreSQLDatabaseProject/
|       +-- PostgreSQLDatabaseProject.vstemplate
|       +-- Project.pgproj
+-- tests/
    +-- PgDatabaseProject.Core.Tests/
    +-- PgDatabaseProject.SchemaExtractor.Tests/
```

---

## 10. Orden de Ejecucion Recomendado

```
Fase 1 (Core + SchemaExtractor)
  |
  |  1.1  Crear solution y estructura de proyectos
  |  1.2  Implementar Core (modelos + interfaces)
  |  1.3  Implementar SchemaExtractor con todos los extractors
  |  1.4  Tests unitarios + test de integracion con Testcontainers
  |  1.5  Validar extraccion con una DB PostgreSQL real
  |
  v
Fase 2 (Project System)
  |
  |  2.1  Configurar CPS project type (.pgproj)
  |  2.2  Definir MSBuild props/targets
  |  2.3  Crear XAML rules para propiedades
  |  2.4  Crear project template (.vstemplate)
  |  2.5  Probar creacion de proyecto en VS experimental instance
  |
  v
Fase 3 (Extension + Import Wizard)
  |
  |  3.1  Crear VSPackage y registrar comandos
  |  3.2  Implementar dialogo WPF del wizard (3 pasos)
  |  3.3  Integrar SchemaExtractor con el wizard
  |  3.4  Auto-agregar archivos importados al .pgproj
  |  3.5  Test end-to-end: crear proyecto -> importar -> ver scripts en VS
  |
  v
Fase 4 (Pulido)
      4.1  Refresh selectivo
      4.2  Diff antes de sobrescribir
      4.3  Persistencia segura de conexion
      4.4  Iconos custom en Solution Explorer
      4.5  Documentacion de usuario
```
