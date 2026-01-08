# /backend/plugins/task_manager/logic.py
import logging
import json
from datetime import datetime
import asyncio
from .notifications import send_notification

logger = logging.getLogger("mozaiks_core.plugins.task_manager")

# In-memory task storage (in a real implementation, this would use the database)
tasks = {}

async def execute(data):
    """Main entry point for the plugin execution"""
    action = data.get("action", "")
    user_id = data.get("user_id", "")
    
    logger.info(f"Task Manager executing action: {action} for user {user_id}")
    
    if not user_id:
        return {"error": "User ID is required"}
    
    # Initialize user tasks if they don't exist
    if user_id not in tasks:
        tasks[user_id] = []
    
    # Handle task operations
    if action == "get_tasks":
        return {"tasks": tasks[user_id]}
    
    elif action == "add_task":
        task_data = data.get("task", {})
        if not task_data.get("title"):
            return {"error": "Task title is required"}
        
        task = {
            "id": f"task_{len(tasks[user_id]) + 1}_{datetime.now().timestamp()}",
            "title": task_data.get("title"),
            "description": task_data.get("description", ""),
            "priority": task_data.get("priority", "medium"),
            "due_date": task_data.get("due_date"),
            "completed": False,
            "created_at": datetime.now().isoformat()
        }
        
        tasks[user_id].append(task)
        
        # Send notification about task creation
        send_notification(
            user_id=user_id,
            title="New Task Created",
            message=f"You've created a new task: {task['title']}",
            metadata={"task": task}
        )
        
        return {"success": True, "task": task}
    
    elif action == "complete_task":
        task_id = data.get("task_id")
        if not task_id:
            return {"error": "Task ID is required"}
        
        found = False
        for task in tasks[user_id]:
            if task["id"] == task_id:
                task["completed"] = True
                task["completed_at"] = datetime.now().isoformat()
                found = True
                
                # Send notification about task completion
                send_notification(
                    user_id=user_id,
                    title="Task Completed",
                    message=f"You've completed the task: {task['title']}",
                    metadata={"task": task}
                )
                break
        
        if not found:
            return {"error": "Task not found"}
        
        return {"success": True}
    
    elif action == "delete_task":
        task_id = data.get("task_id")
        if not task_id:
            return {"error": "Task ID is required"}
        
        # Find the task before deletion
        deleted_task = None
        for task in tasks[user_id]:
            if task["id"] == task_id:
                deleted_task = task
                break
        
        original_count = len(tasks[user_id])
        tasks[user_id] = [t for t in tasks[user_id] if t["id"] != task_id]
        
        if len(tasks[user_id]) == original_count:
            return {"error": "Task not found"}
        
        # If task was found and deleted, send notification
        if deleted_task:
            send_notification(
                user_id=user_id,
                title="Task Deleted",
                message=f"You've deleted the task: {deleted_task['title']}",
                metadata={"task": deleted_task}
            )
        
        return {"success": True}
    
    else:
        return {"error": f"Unknown action: {action}"}