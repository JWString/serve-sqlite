# serve-sqlite
A simple utility to host a personal REST API using a SQLite data source

This utility is available as a global .Net tool for .Net 7

To install, run:
```
dotnet tool install --global --version 1.0.0 serve-sqlite
```

To start a local server using a sqlite data store:
```
serve-sqlite --path <path-to-your-sqlite-db-file>
```

The last example will start a server that binds to ports automatically.  To specify ports, use the `--http` or `--https` options.

Note: this tool is intended for personal and development use.  As of version 0.1.0 CORS is enabled by default to make this easier.  To disable CORS, use the `--no-cors` option.  
