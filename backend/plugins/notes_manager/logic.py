# /backend/plugins/notes_manager/logic.py
import logging
from datetime import datetime
from core.notifications_manager import notifications_manager

logger = logging.getLogger("mozaiks_core.plugins.notes_manager")

# In-memory notes storage (replace this with your DB calls in production)
notes = {}

async def execute(data):
    action = data.get("action", "")
    user_id = data.get("user_id", "")
    
    logger.info(f"Notes Manager executing action: {action} for user {user_id}")
    
    if not user_id:
        return {"error": "User ID is required"}

    # Initialize user notes storage
    if user_id not in notes:
        notes[user_id] = []

    if action == "get_notes":
        user_notes = notes[user_id]

        # Filter by category
        category = data.get("filter_category")
        if category and category != "all":
            user_notes = [note for note in user_notes if note.get("category") == category]

        # Sorting
        sort_by = data.get("sort_by", "created_at")
        sort_direction = data.get("sort_direction", "desc")

        sorted_notes = sorted(
            user_notes,
            key=lambda note: note.get(sort_by, "").lower() if sort_by == "title" else note.get(sort_by, ""),
            reverse=(sort_direction == "desc")
        )

        return {"notes": sorted_notes}

    elif action == "add_note":
        note_data = data.get("note", {})
        if not note_data.get("title"):
            return {"error": "Note title is required"}

        note_id = f"note_{len(notes[user_id]) + 1}_{datetime.now().timestamp()}"

        note = {
            "id": note_id,
            "title": note_data.get("title"),
            "content": note_data.get("content", ""),
            "category": note_data.get("category", "general"),
            "color": note_data.get("color", "#ffffff"),
            "pinned": note_data.get("pinned", False),
            "created_at": datetime.now().isoformat(),
            "updated_at": datetime.now().isoformat()
        }

        notes[user_id].append(note)

        # Trigger notification using centralized logic
        await notifications_manager.create_notification(
            user_id=user_id,
            notification_type="notes_manager_note_created",
            title="New Note Created",
            message=f"You've created a new note: {note['title']}",
            metadata={"note_id": note["id"], "category": note["category"]}
        )

        return {"success": True, "note": note}

    elif action == "update_note":
        note_data = data.get("note", {})
        note_id = note_data.get("id")
        
        if not note_id:
            return {"error": "Note ID is required"}

        existing_note = next((n for n in notes[user_id] if n["id"] == note_id), None)
        if not existing_note:
            return {"error": "Note not found"}

        content_changed = existing_note["content"] != note_data.get("content", existing_note["content"])

        existing_note.update({
            "title": note_data.get("title", existing_note["title"]),
            "content": note_data.get("content", existing_note["content"]),
            "category": note_data.get("category", existing_note["category"]),
            "color": note_data.get("color", existing_note["color"]),
            "pinned": note_data.get("pinned", existing_note["pinned"]),
            "updated_at": datetime.now().isoformat()
        })

        # Trigger notification if content changed
        if content_changed:
            await notifications_manager.create_notification(
                user_id=user_id,
                notification_type="notes_manager_note_updated",
                title="Note Updated",
                message=f"You've updated your note: {existing_note['title']}",
                metadata={"note_id": existing_note["id"], "category": existing_note["category"]}
            )

        return {"success": True, "note": existing_note}

    elif action == "delete_note":
        note_id = data.get("note_id")
        
        if not note_id:
            return {"error": "Note ID is required"}

        note_to_delete = next((note for note in notes[user_id] if note["id"] == note_id), None)
        if not note_to_delete:
            return {"error": "Note not found"}

        notes[user_id].remove(note_to_delete)

        # Trigger notification for note deletion
        await notifications_manager.create_notification(
            user_id=user_id,
            notification_type="notes_manager_note_deleted",
            title="Note Deleted",
            message=f"You've deleted the note: {note_to_delete['title']}",
            metadata={"note_id": note_to_delete["id"], "category": note_to_delete["category"]}
        )

        return {"success": True}

    else:
        return {"error": f"Unknown action: {action}"}
