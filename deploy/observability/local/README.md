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
kubectl -n royale-observability get pods,svc,pvc
```

Forward local ports as needed:

```sh
kubectl -n royale-observability port-forward svc/grafana 3000:3000
kubectl -n royale-observability port-forward svc/prometheus 9090:9090
kubectl -n royale-observability port-forward svc/loki 3100:3100
kubectl -n royale-observability port-forward svc/tempo 3200:3200
kubectl -n royale-observability port-forward svc/otel-collector 4317:4317
kubectl -n royale-observability port-forward svc/otel-collector 4318:4318
```

Point a local Royale server at the Collector with:

```sh
OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4317 \
dotnet run --project src/Royale.Server/Royale.Server.csproj -- --map graybox --run-ticks 300
```

Keep secrets, host-specific storage classes, resource overrides, and local port
policy in `kustomization.yaml` or ignored files under this directory.
