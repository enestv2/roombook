# Architecture

## System overview

Roombook is a web application for finding available rooms and creating conflict-free reservations.
V1 uses a modular monolith: one deployable ASP.NET Core 10 application with explicit domain
boundaries and a React client. PostgreSQL is the system of record. This keeps deployment and
operations simple for a small team while preserving boundaries for later extraction.

## Modules / components and ownership

| Module | Single responsibility | Owns |
|---|---|---|
| Identity & access | Authentication and role checks | Users, roles, credentials |
| Rooms | Room catalog and room configuration | Rooms, capacity, features |
| Availability | Working hours and availability queries | Working schedules, availability rules |
| Reservations | Reservation lifecycle and conflict prevention | Reservations and cancellation state |
| Administration | Management workflows for authorized users | Administrative commands and audit context |

## Communication rules

Modules may call another module only through its public application interface. Reservation
creation must use the availability interface and enforce the conflict check transactionally.
Internal types and storage of another module are not shared. Events are not required in V1.

## Forbidden dependencies (make them testable)

- No module accesses another module's internal types, repositories, or database mappings.
- No module-specific code depends on an external reservation, identity, or paid SaaS service.
- The React client does not connect directly to PostgreSQL.

## Deliberately out of scope

Payments, external calendar/identity integrations, notifications, recurring reservations, advanced
reporting, and microservice deployment are not V1 requirements.
