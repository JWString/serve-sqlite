# serve-sqlite
A simple utility to host a personal REST API using a SQLite data source

This utility is available as a global .Net tool for .Net 6

To install, run:
```
dotnet tool install --global serve-sqlite
```

To start a local server using a sqlite data store:
```
serve-sqlite --path <path-to-your-sqlite-db-file>
```

The last example will start a server that binds to ports automatically.  To specify ports, use the `--http` or `--https` options.