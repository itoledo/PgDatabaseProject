using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PgDatabaseProject.Extension.Services;

internal static class ConnectionStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PgDatabaseProject", "connections.xml");

    public static List<ConnectionEntry> Load()
    {
        var entries = new List<ConnectionEntry>();

        try
        {
            if (!File.Exists(FilePath))
                return entries;

            var doc = XDocument.Load(FilePath);
            foreach (var el in doc.Root?.Elements("Connection") ?? Enumerable.Empty<XElement>())
            {
                entries.Add(new ConnectionEntry
                {
                    Host = (string?)el.Attribute("Host") ?? "localhost",
                    Port = (int?)el.Attribute("Port") ?? 5432,
                    Database = (string?)el.Attribute("Database") ?? "",
                    Username = (string?)el.Attribute("Username") ?? "postgres",
                    Password = (string?)el.Attribute("Password") ?? "",
                    SslMode = (string?)el.Attribute("SslMode") ?? "Prefer"
                });
            }
        }
        catch
        {
            // Corrupt file — return empty list
        }

        return entries;
    }

    public static void Save(IEnumerable<ConnectionEntry> entries)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new XDocument(
                new XElement("Connections",
                    entries.Select(e => new XElement("Connection",
                        new XAttribute("Host", e.Host),
                        new XAttribute("Port", e.Port),
                        new XAttribute("Database", e.Database),
                        new XAttribute("Username", e.Username),
                        new XAttribute("Password", e.Password),
                        new XAttribute("SslMode", e.SslMode)))));

            doc.Save(FilePath);
        }
        catch
        {
            // Best effort — ignore write failures
        }
    }

    public static void AddOrUpdate(ConnectionEntry entry)
    {
        var entries = Load();

        // Remove existing entry with same host/port/database/username
        entries.RemoveAll(e =>
            string.Equals(e.Host, entry.Host, StringComparison.OrdinalIgnoreCase) &&
            e.Port == entry.Port &&
            string.Equals(e.Database, entry.Database, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Username, entry.Username, StringComparison.OrdinalIgnoreCase));

        // Insert at the top (most recent first)
        entries.Insert(0, entry);

        // Keep max 20 entries
        if (entries.Count > 20)
            entries.RemoveRange(20, entries.Count - 20);

        Save(entries);
    }

    public static void Remove(ConnectionEntry entry)
    {
        var entries = Load();
        entries.RemoveAll(e =>
            string.Equals(e.Host, entry.Host, StringComparison.OrdinalIgnoreCase) &&
            e.Port == entry.Port &&
            string.Equals(e.Database, entry.Database, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Username, entry.Username, StringComparison.OrdinalIgnoreCase));
        Save(entries);
    }
}
