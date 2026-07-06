# Claude Code Prompts — Critical Roadmap for Healthcare Appointment System

These prompts are based on the current implementation in the repository and are designed to be executed one-by-one in order.

---

## Prompt 1 — Implement real server-side pagination for appointments, patients, and payments

You are working in the Healthcare Appointment System repository. The current API controllers for appointments, patients, and payments retrieve full collections and then paginate in memory. This is not scalable and does not match the intended production behavior.

Context:

- The controllers in [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/AppointmentsController.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/AppointmentsController.cs), [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/PatientsController.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/PatientsController.cs), and [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/PaymentsController.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/PaymentsController.cs) use `GetAllAsync()` and then apply paging in memory.
- The repository interfaces currently expose `IEnumerable<T>`-based retrieval.
- Keep the existing API contract with `pageNumber` and `pageSize` intact.

Tasks:

1. Add repository methods that support database-level paging, such as `GetPagedAsync(pageNumber, pageSize, ...)`.
2. Implement EF Core versions that use `IQueryable.Skip/Take` and `CountAsync()` before materializing results.
3. Update the controllers to use the new repository methods and preserve the current response shape.
4. Add or update unit/integration tests to verify correct paging behavior and total count.

Acceptance criteria:

- No controller performs in-memory pagination over the full dataset.
- The EF Core implementation uses SQL-level paging.
- The API returns correct items, page count, and total count.
- Existing pagination query parameters continue to work.

---

## Prompt 2 — Add a Stripe webhook endpoint for payment reconciliation

You are working in the Healthcare Appointment System repository. The existing payment flow depends on a synchronous API call after Stripe confirmation, but there is no webhook endpoint to reconcile payments on the server side.

Context:

- The payment gateway configuration already includes a webhook secret in [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Adapters/Payments/StripeSettings.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Adapters/Payments/StripeSettings.cs).
- The payment processing flow is implemented in the application layer, and the domain already has a guard for duplicate/successful payments.
- The current backend does not expose a Stripe webhook endpoint in the API controllers.

Tasks:

1. Add a webhook endpoint such as `/api/v1/webhooks/stripe`.
2. Verify the `Stripe-Signature` header using the configured webhook secret.
3. Handle at least `payment_intent.succeeded` and `payment_intent.payment_failed` events.
4. Reconcile the payment state idempotently so duplicate deliveries do not create inconsistent state.
5. Ensure the webhook flow reuses the existing payment handler logic rather than duplicating it.

Acceptance criteria:

- The endpoint validates Stripe signatures.
- Successful and failed payment events are processed correctly.
- Repeated webhook deliveries do not corrupt state.
- The app can recover from payment confirmation events even if the browser session closes before the client-side API call arrives.

---

## Prompt 3 — Replace the in-process appointment reference code generator with a multi-instance-safe approach

You are working in the Healthcare Appointment System repository. The current `AppointmentCodeGenerator` is a thread-safe singleton within one process, but it does not work safely across multiple app instances.

Context:

- The implementation lives in [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Domain/Services/AppointmentCodeGenerator.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Domain/Services/AppointmentCodeGenerator.cs).
- The appointment reference code is stored with a unique DB index in the EF Core model.
- The current implementation resets per process and can produce collisions when multiple API instances run behind a load balancer.

Tasks:

1. Replace or backstop the current singleton-based approach with a multi-instance-safe strategy.
2. Prefer a database-backed sequence or a Redis-backed increment mechanism if appropriate.
3. Make sure the system handles collisions gracefully and does not crash on duplicate reference codes.
4. Add tests that cover the collision scenario or the new generation strategy.

Acceptance criteria:

- Two independent app instances cannot generate the same reference code for the same time window.
- The app handles generation failure or collision without leaving the appointment flow in a broken state.
- The new implementation is compatible with the existing domain service interface.

---

## Prompt 4 — Gate Swagger UI and XML documentation behind development-safe settings

You are working in the Healthcare Appointment System repository. Swagger is currently exposed in all environments, and the XML documentation file may not be included in Swagger output.

Context:

- The Swagger middleware is registered unconditionally in [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Program.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Program.cs).
- The API project may need `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in its project file so XML comments are emitted.
- The current setup uses `IncludeXmlComments` only if the XML file exists.

Tasks:

1. Only enable Swagger UI in development or when explicitly configured.
2. Protect the API surface from being publicly browsable in non-development environments.
3. Ensure the project emits XML documentation and that Swagger includes the documented summaries/examples where appropriate.
4. Keep the behavior consistent for local development and production.

Acceptance criteria:

- Swagger UI is not exposed in production by default.
- XML docs are emitted and included in Swagger when available.
- The change is configuration-driven and easy to reason about.

---

## Prompt 5 — Make the CI vulnerability scan fail the build and update vulnerable packages

You are working in the Healthcare Appointment System repository. The current CI workflow reports vulnerable packages but does not fail the build, so security issues can slip through.

Context:

- The repo already has a vulnerability scan step, but it uses `continue-on-error: true` and therefore does not act as a hard gate.
- The review flagged MailKit as a package that should be reviewed and upgraded.

Tasks:

1. Change the CI scan so High/Critical vulnerabilities fail the build.
2. Review the existing package references and upgrade any known vulnerable package versions.
3. Ensure the pipeline behavior is explicit and visible in the workflow output.
4. Keep the change scoped to the backend CI setup unless a repository-wide dependency update is required.

Acceptance criteria:

- The CI pipeline fails when a High/Critical vulnerability is detected.
- Vulnerable packages are updated or explicitly justified.
- The workflow output clearly communicates the security gate result.

---

## Prompt 6 — Align doctors CRUD with the existing CQRS pattern

You are working in the Healthcare Appointment System repository. The doctor creation/deactivation flow currently bypasses the same command/handler pattern used for appointments, patients, and payments.

Context:

- The doctor endpoints in [Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/DoctorsController.cs](Healthcare-Appointment-Notification-System/Healthcare.AppointmentSystem/Healthcare.Presentation.API/Controllers/DoctorsController.cs) perform the business logic directly inside the controller.
- Other aggregates already follow a command-handler flow and have dedicated tests.

Tasks:

1. Introduce command and handler classes for doctor creation and doctor deactivation.
2. Move the business logic out of the controller and into the handlers.
3. Keep the controller behavior and API responses the same.
4. Add unit tests for the new handler behavior.

Acceptance criteria:

- The controller no longer contains the core doctor business logic.
- Doctor flows follow the same pattern as the other aggregates.
- Tests cover success and validation/error cases.

---

## Suggested execution order

1. Server-side pagination
2. Stripe webhook reconciliation
3. Multi-instance-safe appointment codes
4. Swagger/documentation hardening
5. CI vulnerability gate
6. Doctors CQRS alignment
