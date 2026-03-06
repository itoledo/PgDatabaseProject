using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace PgDatabaseProject.Extension;

[Export(typeof(IProjectTreePropertiesProvider))]
[AppliesTo("PostgreSQLDatabaseProject")]
[Order(1000)]
internal sealed class PgProjectIconProvider : IProjectTreePropertiesProvider
{
    public void CalculatePropertyValues(
        IProjectTreeCustomizablePropertyContext propertyContext,
        IProjectTreeCustomizablePropertyValues propertyValues)
    {
        if (propertyValues.Flags.Contains(ProjectTreeFlags.Common.ProjectRoot))
        {
            propertyValues.Icon = KnownMonikers.Database.ToProjectSystemType();
            propertyValues.ExpandedIcon = KnownMonikers.Database.ToProjectSystemType();
        }
    }
}
