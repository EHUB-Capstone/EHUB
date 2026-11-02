# AI allocation UI demo

Hosts the **actual EHub.Api entry point** on loopback `127.0.0.1:5226` with a new disposable pgvector PostgreSQL container. The regular frontend at `http://127.0.0.1:5174/admin/subjects` keeps its normal API calls, login and admin policies. This tool does not connect to an existing application database or import personal identities. Strict CORS allows the local frontend origins at `localhost:5173`, `127.0.0.1:5174` and `localhost:5174`.

```powershell
dotnet run --project tools/mentor-matching/ui-demo/UiDemo.csproj -- --demo100 --profiles <deidentified-profiles.jsonl> --projects <100-projects.json> --model <model.json> --output artifacts/mentor-ui-demo --verify
```

To include ten fictional IT mentors, add `--it-profiles tools/mentor-matching/ui-demo/it-mentors.json`. These profiles use the existing Academic/IT slot, have synthetic professional histories and demo-only identities, and are active participants in the semester. Each new host starts a fresh disposable database, so this mode previews both slots together: **100 Enterprise + 100 Academic = 200 proposals** in one all-classes request. No previous run's assignments are deleted or reassigned in place.

Run from inside the repository. Docker Desktop must be available and the cached `pgvector/pgvector:0.8.6-pg16` image must be usable. The API port is fixed to loopback 5226; stop another local API using that port first. `Ctrl+C` shuts down the demo host and disposes only its dedicated database container.

Inputs:

- Exactly 30 distinct profile snapshots with anonymous `mentorRef` (`M001` etc.), `mentorType: Enterprise`, and the existing `profile` professional fields. The host maps no real names or contacts into users.
- Exactly 100 distinct projects in `{ "projects": [{ "projectId": "P001", "title": "...", "description": "..." }] }` or an equivalent top-level array; `id` is also accepted instead of `projectId`. Projects are placed into ten EXE101 classes with ten teams each, inside the clearly labeled active FA2026 semester.
- A valid portable trained ranking artifact. It is read locally and never rewritten.
- Optional `--it-profiles`: a JSON array of exactly ten distinct `IT001`-style references with `mentorType: Academic` and `profile.mentorType: IT`. The included `it-mentors.json` contains only invented professional profiles, without project-specific answers or matching labels.

The explicit `--demo100` flag is mandatory. Output must stay within this repository's Git-ignored `artifacts/mentor-ui-demo` folder. `ready.json` contains identifiers, aggregate seed counts and hashes, without tokens or private credentials.

Normal UI login uses the deliberately disposable fixture account **admin.ai-demo@example.test** with password **AiDemoOnly@2026!**. This account exists only in the newly created demo database; it must never be reused in another environment. The JWT signing key is generated per process and stays in memory. No authentication or authorization is bypassed.

The demo uses the Testing environment and clears configuration sources before supplying its own in-memory settings. It does not load Development/User Secrets or `.env`. Background workers and calls to external email, storage, Google and embedding providers are blocked, while the real offline trained ranker remains enabled. Preview/accept still use the real API handler, transaction, concurrency checks and Outbox persistence.

For admin CV upload UI tests, mentor documents use process-local memory storage through `IMentorDocumentStorageService`; no external storage is contacted. Upload validation, DOCX reading, professional extraction, authorization and database updates still use the real application handlers. Original uploaded bytes can be downloaded during the demo process lifetime. Restarting the disposable host clears these in-memory files. Use fictional documents for tests; original mentor CVs must not become repository fixtures.

No mentor assignments are created by seeding or `--verify`. With `--verify`, the host checks real HTTP `401`, ordinary admin password login, AI preview with 100 Enterprise proposals across all 100 projects and zero assignments before acceptance. With `--it-profiles`, it also requires 100 Academic proposals, 200 unique team/slot pairs, correct mentor types and no missing slots. It writes aggregate verification results, including the number of score-zero suggestions needing review per type, then stays running for UI interaction.

All 30 supplied deidentified mentors remain **Enterprise**. The extra lecturer account owns the classes and is not an Academic mentor. Without `--it-profiles`, all 100 Academic slots remain visibly unfilled; with it, the ten synthetic Academic/IT profiles fill that pool. Slots with no matching evidence receive a workload-based suggestion with score 0 and an administrator review warning. Allocation considers documented specialties across business and technology without changing mentor types. Review the preview in the UI and accept only the concrete proposal you intend to save into this disposable database.
