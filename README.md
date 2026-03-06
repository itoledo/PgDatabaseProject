# PostgresDatabaseProject

Very simple Visual Studio extension to add a PostgreSQL database project to your solution. Inspired by the SQL Server Database Project extension, but for PostgreSQL.

It allows you to manage your database schema as code. Useful for version control, collaboration and for feeding your database objects to your AI minions. You can import an existing database schema into the project, and it will create a .sql file for each database object (tables, views, functions, etc.) in the project.

## Features

- Allows importing an existing PostgreSQL database schema into a project. It creates a .sql file for each database object (tables, views, functions, etc.) in the project.
- Syntax coloring.
- Running scripts from VS to update the database.
 
## Planned features

- Grid view for results.
- Schema comparison between the project and a live database.
