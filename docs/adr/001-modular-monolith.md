# ADR 001: Modular monolith

Status: accepted for initial implementation. Date: 2026-09-16.

## Context

Sales and purchasing must update stock, cost, balances and audit records atomically. The initial product has no demonstrated requirement for independently deployed services.

## Decision

Use React/TypeScript/Vite, one ASP.NET Core application organized into domain/application/infrastructure/API layers, and PostgreSQL through EF Core/Npgsql. Use a durable database-backed worker for jobs. Retain module boundaries inside one solution.

## Alternatives and consequences

Microservices add distributed transaction and operational costs; a spreadsheet backend cannot provide the required access and concurrency controls. A monolith makes transactions and local setup simpler, but demands dependency checks and clear module ownership. Redis, Kubernetes and event streaming require a later evidence-backed decision.

## Verification

Phase 01 pins supported versions, builds the solution and proves healthy web/API/database startup. Architecture tests enforce layer dependencies. This decision does not select a hosting vendor.
