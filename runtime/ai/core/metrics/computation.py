# backend/core/metrics/computation.py
from __future__ import annotations

from datetime import date, datetime, timedelta
from typing import Any, Mapping, MutableMapping


def safe_div(n: float, d: float) -> float:
    return float(n) / float(d if d else 1)


def add_timestamp_range(query: MutableMapping[str, Any], *, start: datetime | None, end: datetime | None) -> None:
    if start is None and end is None:
        return

    bounds: dict[str, Any] = {}
    if start is not None:
        bounds["$gte"] = start
    if end is not None:
        bounds["$lt"] = end
    query["timestamp"] = bounds


async def distinct_users(collection, query: Mapping[str, Any], *, field: str = "userId") -> int:
    pipeline = [
        {"$match": dict(query)},
        {"$group": {"_id": f"${field}"}},
        {"$count": "count"},
    ]
    cursor = collection.aggregate(pipeline, allowDiskUse=True)
    docs = await cursor.to_list(length=1)
    return int(docs[0]["count"]) if docs else 0


async def distinct_users_by_event(
    collection,
    *,
    app_id: str,
    event_type: str,
    start: datetime | None = None,
    end: datetime | None = None,
) -> int:
    query: dict[str, Any] = {"appId": app_id, "type": event_type}
    add_timestamp_range(query, start=start, end=end)
    return await distinct_users(collection, query)


async def count_events(collection, query: Mapping[str, Any]) -> int:
    return int(await collection.count_documents(dict(query)))


async def count_events_by_event(
    collection,
    *,
    app_id: str,
    event_type: str,
    start: datetime,
    end: datetime,
) -> int:
    return await count_events(
        collection,
        {"appId": app_id, "type": event_type, "timestamp": {"$gte": start, "$lt": end}},
    )


async def cohort_retention(
    collection,
    *,
    app_id: str,
    cohort_days_ago: int,
    active_start: datetime,
    active_end: datetime,
    cohort_anchor_date: date | None = None,
    signup_event_type: str = "UserSignedUp",
    active_event_type: str = "UserActive",
) -> float:
    anchor = cohort_anchor_date or active_end.date()
    cohort_date = anchor - timedelta(days=cohort_days_ago)

    cohort_day = cohort_date.isoformat()
    cohort_count = await distinct_users(collection, {"appId": app_id, "type": signup_event_type, "day": cohort_day})
    if cohort_count == 0:
        return 0.0

    active_query: dict[str, Any] = {"appId": app_id, "type": active_event_type}
    add_timestamp_range(active_query, start=active_start, end=active_end)

    pipeline = [
        {"$match": active_query},
        {"$group": {"_id": "$userId"}},
        {
            "$lookup": {
                "from": collection.name,
                "let": {"uid": "$_id"},
                "pipeline": [
                    {
                        "$match": {
                            "$expr": {
                                "$and": [
                                    {"$eq": ["$appId", app_id]},
                                    {"$eq": ["$type", signup_event_type]},
                                    {"$eq": ["$day", cohort_day]},
                                    {"$eq": ["$userId", "$$uid"]},
                                ]
                            }
                        }
                    },
                    {"$limit": 1},
                ],
                "as": "cohort",
            }
        },
        {"$match": {"cohort.0": {"$exists": True}}},
        {"$count": "count"},
    ]

    cursor = collection.aggregate(pipeline, allowDiskUse=True)
    docs = await cursor.to_list(length=1)
    active_cohort_count = int(docs[0]["count"]) if docs else 0

    return safe_div(active_cohort_count, cohort_count)
