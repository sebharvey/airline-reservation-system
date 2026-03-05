Add a new timetable section to `res/schedules/flight-schedule-2026.md` using the details provided in $ARGUMENTS.

**Important:** Always append a new timetable section to the file. Do NOT check whether a section with the same name already exists, and do NOT update or replace any existing section. Every invocation of this command must result in a new section being added, even if a section with the same name is already present in the file.

The new section should follow the existing format used in the file:

```
## <Region Name> (<Flight Number Block>)

| Flight | Route | Direction | Departure | Arrival | Days | Aircraft |
|--------|-------|-----------|-----------|---------|------|----------|
| <flight rows> |
```

Append the new section at the end of the file, before the "Aircraft Assignment Rationale" and "Flight Number Block Reference" tables if they exist, or at the very end of the file.
