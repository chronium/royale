# Development Launch Scripts

Build the solution before using these scripts. They run existing build output
without restoring or rebuilding and may be invoked from any working directory.

```sh
launch/editor.sh
launch/server.sh graybox
launch/client-offline.sh
launch/client-connected.sh
```

The server requires a map ID. Every script forwards additional arguments to its
application, so existing CLI options can override profile values:

```sh
launch/server.sh prototype-arena --port 7788
launch/client-offline.sh --map prototype-arena
launch/client-connected.sh --connect 192.0.2.10 --port 7788 --map prototype-arena
launch/editor.sh --project /path/to/arena.royaleproject
```

All scripts export `OTEL_EXPORTER_OTLP_ENDPOINT`, defaulting to
`http://127.0.0.1:4317`. Set it before launching to use another collector.
