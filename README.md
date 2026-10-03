# Mini MES

A small manufacturing execution system for a battery-electrode line: work orders, lot and carrier tracking, track-in/output/track-out at the operator station, and lot genealogy.

## Quickstart

Prerequisite: [Docker](https://www.docker.com/) (Docker Desktop or Docker Engine with Compose).

```bash
docker compose up --build
```

Open <http://localhost:8080>. The first start builds the images, applies the database migrations and seeds demo data.

If host port 5432 is already in use, set another one for the PostgreSQL mapping, for example `POSTGRES_PORT=55432 docker compose up --build`.

The equipment simulator starts with the stack as its own `simulator` service and feeds live readings and alarms to the API. It authenticates with a shared machine key: the default is a demo value, set `MACHINE_API_KEY` (at least 16 characters) to use your own for both the API and the simulator. To run the simulator outside Docker against a locally running API, use `dotnet run --project backend/src/MiniMes.Simulator`: its launch profile sets `DOTNET_ENVIRONMENT=Development`, which loads `appsettings.Development.json` (hub on `localhost:5080` and the demo key).

### Demo users

The login page has a "Log in as" card for each user, one click and no password. To log in with a password, use the default demo password `demo-pass` (override it with `DEMO_PASSWORD`).

| User       | Role     |
| ---------- | -------- |
| `planner`  | Planner  |
| `operator` | Operator |
| `qc`       | QC       |
| `admin`    | Admin    |

Stop with `docker compose down`; add `-v` to also drop the database volume and reseed from scratch.

## Tests

Integration tests start PostgreSQL through Testcontainers, so Docker must be running.

```bash
dotnet test backend
npm ci --prefix frontend
npm test --prefix frontend -- --run
```
