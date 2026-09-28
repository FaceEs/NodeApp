# NodeWorker API

Minimal ASP.NET Core 10 API for parsing HTML supplied as base64, extracting CSS-selected attributes and email addresses, decrypting AES-256/ECB data, and storing selected elements in PostgreSQL.

## Run

From the project root, run `docker compose up --build`. The API builds and starts in its mounted .NET 10 SDK container at `http://localhost:8090`. Swagger UI is available at `http://localhost:8090/api/swagger`.

pgAdmin opens at `http://localhost:8080` without an application login. The `NodeWorker PostgreSQL` server is registered automatically. PostgreSQL 18 data is stored in the `postgres_data` named volume.

The API endpoint is `POST http://localhost:8090/api/process`. Example request bodies are in `json_payload_1.txt` and `json_payload_2.txt`; these compact self-contained examples share an HTML page and exercise the `a[href]` and `script[src]` selectors. Their corresponding formatted responses are in `json_result_1.txt` and `json_result_2.txt`.

The PostgreSQL credentials in `compose.yml` and `servers.json` are development-only values for this test project. The `elements` table is created by the application at startup. Each matching element is inserted with its selected attribute value and complete outer HTML.

## Async notes

HTML parsing and PostgreSQL I/O use asynchronous APIs, as does AES stream decryption. .NET's regular-expression matching API is synchronous because matching is CPU-bound and has no asynchronous counterpart; wrapping this small operation in `Task.Run` would add thread-pool overhead without freeing the request thread from useful I/O.

## Publishing

A public GitHub repository must be created under the user's own account and pushed with that account's credentials. This workspace is prepared for that step but cannot publish without account authorization.
