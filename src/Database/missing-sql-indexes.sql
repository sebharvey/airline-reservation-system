/*
    Generate CREATE INDEX statements for missing indexes
    recommended by SQL Server's Missing Index DMVs.

    Notes:
    - Review each statement before executing.
    - The DMVs are reset on SQL Server restart, so results
      reflect activity since the last start-up.
    - Recommendations are heuristic; validate against workload
      and existing indexes before applying.
    - Requires VIEW SERVER STATE permission.
*/

SELECT
    [Database]          = DB_NAME(mid.database_id),
    [Schema]            = OBJECT_SCHEMA_NAME(mid.object_id, mid.database_id),
    [Table]             = OBJECT_NAME(mid.object_id, mid.database_id),
    [ImpactScore]       = CONVERT(DECIMAL(18,2),
                            migs.avg_total_user_cost
                          * migs.avg_user_impact
                          * (migs.user_seeks + migs.user_scans)),
    [UserSeeks]         = migs.user_seeks,
    [UserScans]         = migs.user_scans,
    [AvgUserImpact]     = migs.avg_user_impact,
    [AvgTotalUserCost]  = migs.avg_total_user_cost,
    [LastUserSeek]      = migs.last_user_seek,
    [CreateIndexStatement] =
        N'CREATE NONCLUSTERED INDEX [IX_'
        + OBJECT_NAME(mid.object_id, mid.database_id)
        + N'_'
        + REPLACE(REPLACE(REPLACE(REPLACE(
              ISNULL(mid.equality_columns, N'')
            + CASE
                WHEN mid.equality_columns IS NOT NULL
                 AND mid.inequality_columns IS NOT NULL
                THEN N'_'
                ELSE N''
              END
            + ISNULL(mid.inequality_columns, N''),
            N'[', N''), N']', N''), N', ', N'_'), N' ', N'')
        + N'] ON '
        + mid.statement
        + N' ('
        + ISNULL(mid.equality_columns, N'')
        + CASE
            WHEN mid.equality_columns IS NOT NULL
             AND mid.inequality_columns IS NOT NULL
            THEN N', '
            ELSE N''
          END
        + ISNULL(mid.inequality_columns, N'')
        + N')'
        + ISNULL(N' INCLUDE (' + mid.included_columns + N')', N'')
        + N' WITH (ONLINE = ON, FILLFACTOR = 90);'
FROM sys.dm_db_missing_index_details        AS mid
INNER JOIN sys.dm_db_missing_index_groups   AS mig
    ON mig.index_handle = mid.index_handle
INNER JOIN sys.dm_db_missing_index_group_stats AS migs
    ON migs.group_handle = mig.index_group_handle
WHERE mid.database_id = DB_ID()        -- current database only; remove to widen scope
ORDER BY [ImpactScore] DESC;
