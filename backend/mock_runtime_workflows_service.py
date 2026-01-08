"""A tiny mock of MozaiksAI Runtime workflow discovery.

Used only for local schema alignment and manual testing of the Pack Loader proxy.
"""

from fastapi import FastAPI

app = FastAPI()

@app.get("/api/workflows/{app_id}/available")
async def available(app_id: str, user_id: str):
    # Simulate user-specific gating by varying response
    base = [
        {
            "id": "generator",
            "name": "Generator",
            "description": "Auto Tool Mode Generator",
            "icon": "sparkles",
            "gated": False,
        },
        {
            "id": "chat",
            "name": "Chat",
            "description": "Standard Chat",
            "icon": "message-circle",
            "gated": False,
        },
    ]

    if user_id.endswith("pro"):
        base.append(
            {
                "id": "insights",
                "name": "Insights",
                "description": "Premium insights workflow",
                "icon": "bar-chart",
                "gated": False,
            }
        )

    return {
        "app_id": app_id,
        "user_id": user_id,
        "workflows": base,
    }
