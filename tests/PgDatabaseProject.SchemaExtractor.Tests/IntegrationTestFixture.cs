using Testcontainers.PostgreSql;

namespace PgDatabaseProject.SchemaExtractor.Tests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await SeedDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task SeedDatabaseAsync()
    {
        using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        using var cmd = new Npgsql.NpgsqlCommand(SeedSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private const string SeedSql = """
        -- Schemas
        CREATE SCHEMA IF NOT EXISTS app;

        -- Extensions
        CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

        -- Types
        CREATE TYPE public.status_type AS ENUM ('active', 'inactive', 'pending');
        CREATE TYPE app.address AS (
            street text,
            city text,
            zip_code varchar(10)
        );
        CREATE DOMAIN public.positive_int AS integer CHECK (VALUE > 0);

        -- Sequences
        CREATE SEQUENCE public.order_seq
            AS bigint
            INCREMENT BY 1
            MINVALUE 1
            MAXVALUE 9999999
            START WITH 1000
            NO CYCLE;

        -- Tables
        CREATE TABLE public.users (
            id serial PRIMARY KEY,
            username varchar(100) NOT NULL,
            email varchar(255) NOT NULL UNIQUE,
            status public.status_type NOT NULL DEFAULT 'active',
            created_at timestamp NOT NULL DEFAULT now()
        );

        CREATE TABLE public.orders (
            id bigint NOT NULL DEFAULT nextval('public.order_seq'),
            user_id integer NOT NULL REFERENCES public.users(id),
            total numeric(12, 2) NOT NULL,
            created_at timestamp NOT NULL DEFAULT now(),
            CONSTRAINT orders_pkey PRIMARY KEY (id),
            CONSTRAINT orders_total_positive CHECK (total >= 0)
        );

        CREATE TABLE app.config (
            key varchar(100) PRIMARY KEY,
            value text NOT NULL,
            updated_at timestamp NOT NULL DEFAULT now()
        );

        -- Views
        CREATE VIEW public.active_users AS
            SELECT id, username, email
            FROM public.users
            WHERE status = 'active';

        -- Functions
        CREATE FUNCTION public.get_user_count()
            RETURNS bigint
            LANGUAGE sql
            STABLE
        AS $$
            SELECT count(*) FROM public.users;
        $$;

        CREATE FUNCTION public.get_user_by_id(p_id integer)
            RETURNS TABLE(id integer, username varchar, email varchar)
            LANGUAGE plpgsql
            STABLE
        AS $$
        BEGIN
            RETURN QUERY
            SELECT u.id, u.username, u.email
            FROM public.users u
            WHERE u.id = p_id;
        END;
        $$;

        -- Procedures
        CREATE PROCEDURE public.deactivate_user(p_user_id integer)
            LANGUAGE plpgsql
        AS $$
        BEGIN
            UPDATE public.users SET status = 'inactive' WHERE id = p_user_id;
        END;
        $$;

        -- Indexes
        CREATE INDEX idx_users_email ON public.users (email);
        CREATE INDEX idx_orders_user_id ON public.orders (user_id);

        -- Triggers
        CREATE FUNCTION public.update_timestamp()
            RETURNS trigger
            LANGUAGE plpgsql
        AS $$
        BEGIN
            NEW.updated_at = now();
            RETURN NEW;
        END;
        $$;

        CREATE TRIGGER trg_config_timestamp
            BEFORE UPDATE ON app.config
            FOR EACH ROW
            EXECUTE FUNCTION public.update_timestamp();
        """;
}
