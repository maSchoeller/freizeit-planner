# Module architecture

Freizeit-Cockpit is one ASP.NET Core deployable with eight domain modules. Each module exposes immutable records and
interfaces through `<Module>.Contracts`; its entities, DbContext, schema, migrations and services remain internal to
`<Module>.Implementation`. The Web host composes workflows but does not become a shared domain model.

The ownership and allowed synchronous calls are maintained in [CONTEXT-MAP.md](../../CONTEXT-MAP.md). Contracts pass
stable IDs and values, never another module's entities or database foreign keys. Cross-module workflows therefore
authorize at the target module and can evolve without sharing tables.

The technical Operations composition is deliberately separate. Migrator applies every module migration in a fixed
order under one PostgreSQL advisory lock. Cleanup calls public retention/erasure contracts and narrows its database
session to `freizeit_jobs`; it does not read another module's tables through an implementation reference.

Atomic workflows are explicit in the Web composition: creating a meal or devotion with a schedule entry is one
transaction, and unlink-or-trash choices are required where deletion crosses ownership. Activity receives only
metadata projections suitable for audit and search, never long text, file content, tokens or blob URLs.
