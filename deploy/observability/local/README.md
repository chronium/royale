# Local Observability Overlay

This directory is for uncommitted machine-specific Kustomize settings.

Create a local overlay:

```sh
cp deploy/observability/local/kustomization.yaml.example deploy/observability/local/kustomization.yaml
```

The base stack uses committed default config files in `deploy/observability/base/config/`.
To customize service configuration locally, copy any local examples you need:

```sh
cp deploy/observability/local/config/prometheus.yml.example deploy/observability/local/config/prometheus.yml
cp deploy/observability/local/config/loki.yaml.example deploy/observability/local/config/loki.yaml
cp deploy/observability/local/config/tempo.yaml.example deploy/observability/local/config/tempo.yaml
cp deploy/observability/local/config/otel-collector.yaml.example deploy/observability/local/config/otel-collector.yaml
```

Then uncomment the matching `configMapGenerator` replacements in the ignored
`kustomization.yaml`. The copied config files are ignored by git.

Render the manifests:

```sh
kubectl kustomize deploy/observability/local
```

Validate against the current Kubernetes client:

```sh
kubectl apply --dry-run=client -k deploy/observability/local
```

Apply to the current local Kubernetes context:

```sh
kubectl apply -k deploy/observability/local
kubectl -n royale-observability rollout status deployment/grafana
kubectl -n royale-observability get pods,svc,pvc
```

Forward local ports as needed:

```sh
deploy/observability/local/port-forward.sh
```

Grafana is available at `http://127.0.0.1:3000` after port-forwarding.
The helper forwards Grafana, Prometheus, Loki, Tempo, and both Collector OTLP
ports. It runs in the foreground and stops all forwards when interrupted.
The base stack provisions the `Royale` dashboard folder automatically with:

- `Royale Server Overview`
- `Royale Networking`
- `Royale Logs and Traces`

It also provisions fixed datasource UIDs for dashboard JSON and Explore links:

- `royale-prometheus`
- `royale-loki`
- `royale-tempo`

Point a local Royale server at the Collector with:

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 \
dotnet run --project src/Royale.Server/Royale.Server.csproj --no-restore -- --map graybox --run-ticks 300
```

After the next Prometheus scrape interval, the server overview dashboard should
show real server gauges and tick-duration metrics. The logs and traces dashboard
should show `royale-server` Loki logs and the `royale.server.run` Tempo trace for
that smoke run.

Keep secrets, host-specific storage classes, resource overrides, and local port
policy in `kustomization.yaml` or ignored files under this directory.
