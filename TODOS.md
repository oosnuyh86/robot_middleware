# TODOS

Deferred work from `/plan-ceo-review` + `/plan-eng-review` on 2026-04-15 for the 송월 테크놀로지 PoC (Approach B).

## P1 — Before Any Production Deployment

### Auth: JWT tokens for backend API + WebSocket

**What:** Add JWT bearer-token authentication to all `/api/*` endpoints + WebSocket relay handshake. Frontend stores token; Unity includes it in REST + WS.

**Why:** PROJECT_STATUS.md:176 flags this as a critical gap. On-site PoC with trusted users is fine, but any multi-tenant or remote deployment needs it.

**Pros:** Unlocks multi-tenant. Required for AWS-hosted deployments. Protects against casual replay attacks on the relay.

**Cons:** Touches every router, signaling server, frontend fetch layer, Unity MiddlewareClient. ~2 day project.

**Context:** Today backend is `app.use(cors())` wide-open. Use `@fastify/jwt`-style middleware. Shared secret or asymmetric key, the latter if preparing for user-role separation. Reference: PROJECT_STATUS.md Integration Points section.

**Effort:** M-L (human), S-M (CC+gstack). Priority P1.

**Depends on:** Nothing — self-contained.

---

## P2 — Debt / Follow-On

### Baseline backend + frontend test coverage for pre-existing endpoints

**What:** Add Vitest tests for companies, professionals, jobs, records CRUD routes. Add React Testing Library tests for dashboard forms + WebRTC hooks.

**Why:** Eng review bootstrapped Vitest+supertest+mongodb-memory-server for CP5/CP7 endpoints. Marginal cost of testing the existing routes is tiny once infra is there.

**Pros:** Catches regressions as schema evolves. Unlocks CI as a gate.

**Cons:** ~3-4 test files for backend, ~5-6 for frontend. Low-risk, low-return but cheap.

**Context:** After eng-review CP5/CP7 backend tests land, add tests for: companies CRUD, professionals CRUD, jobs CRUD, records CRUD + state-transition validation. Frontend: form submission happy + validation paths for each entity.

**Effort:** M (human), S (CC+gstack). Priority P2.

**Depends on:** E-3A (Vitest bootstrap from eng review) must ship first.

---

### Formalize DESIGN.md via /design-consultation

**What:** Run `/design-consultation` to produce a project DESIGN.md covering both the Unity overlay surface (currently HUDTheme.cs) and the web dashboard surface (currently ad-hoc Tailwind + component primitives).

**Why:** Post-PoC expansion (multi-tenant admin UI, onboarding screens, painter registration flows) needs a consistent design system. HUDTheme works for aerospace Unity overlays; dashboard has no explicit system today.

**Pros:** Consistent typography/color/spacing decisions across both surfaces. Cross-designer handoff artifact. Unlocks /plan-design-review running at higher confidence going forward.

**Cons:** ~30min. No immediate blocker.

**Context:** Run when shifting focus from PoC to product. HUDTheme colors + aerospace theme should be captured as canonical tokens in DESIGN.md.

**Effort:** S (human), S (CC+gstack). Priority P2.

**Depends on:** Nothing — self-contained.

---

### S3 upload of RGB+depth keyframes

**What:** Upload local keyframe PNGs referenced by LeRobot HDF5 episodes to S3. Either re-process local records post-hoc, or dual-write local+S3 from the start.

**Why:** D1 deferred S3 upload for PoC — saves bandwidth and local→S3 sync complexity. Post-PoC: datasets need durability and shareability with external ML collaborators.

**Pros:** Durable storage. LeRobot datasets become shareable via pre-signed URL. Enables offsite training.

**Cons:** S3 bandwidth cost scales with record volume. Need retry + partial-upload handling. Adds ~50MB-per-keyframe upload load.

**Context:** D1 explicitly deferred. Implementation path: add `scripts/upload_keyframes_s3.py` sidecar that walks a record's keyframes/ dir and uploads via boto3; update HDF5 manifest to point at S3 URLs; `S3Uploader.cs` has presigned-URL infrastructure. Re-processing old records: single loop over records where `dataset_url` is null.

**Effort:** S (human), S (CC+gstack). Priority P2.

**Depends on:** CP7 sidecar (eng review) lands first.
