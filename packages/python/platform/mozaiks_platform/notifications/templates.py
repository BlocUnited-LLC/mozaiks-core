# backend/core/notifications/templates.py
"""
Notification Template Renderer

Loads templates from notification_templates.json and renders notifications
for different channels with variable substitution.

Templates support:
- Per-channel formatting (in_app, email, sms, push)
- Variable substitution using {{variable}} syntax
- Default templates for unregistered types
- HTML email templates
"""

import os
import json
import logging
import re
from typing import Dict, Any, Optional
from mozaiks_infra.config.config_loader import get_config_path

logger = logging.getLogger("mozaiks_core.notifications.templates")


class TemplateRenderer:
    """
    Renders notification content using templates from configuration.
    
    Features:
    - JSON-based template definitions
    - Multi-channel support
    - Variable substitution
    - Fallback to default templates
    """
    
    def __init__(self):
        self.templates: Dict[str, Any] = {}
        self.digest_templates: Dict[str, Any] = {}
        self._load_templates()
    
    def _load_templates(self):
        """Load templates from configuration file."""
        config_path = get_config_path() / "notification_templates.json"
        
        try:
            if config_path.exists():
                # Explicit encoding avoids Windows default 'charmap' decode failures.
                with open(config_path, "r", encoding="utf-8", errors="replace") as f:
                    data = json.load(f)
                    self.templates = data.get("templates", {})
                    self.digest_templates = data.get("digest_templates", {})
                    logger.info(f"Loaded {len(self.templates)} notification templates")
            else:
                logger.warning(f"Template file not found: {config_path}")
                self._set_defaults()
        except Exception as e:
            logger.error(f"Error loading templates: {e}")
            self._set_defaults()
    
    def _set_defaults(self):
        """Set default templates if config file is missing."""
        self.templates = {
            "_default": {
                "in_app": {
                    "title": "{{title}}",
                    "message": "{{message}}"
                },
                "email": {
                    "subject": "{{title}}",
                    "body_text": "{{message}}",
                    "body_html": "<p>{{message}}</p>"
                },
                "sms": {
                    "body_text": "{{title}}: {{message}}"
                },
                "push": {
                    "title": "{{title}}",
                    "body": "{{message}}"
                }
            }
        }
    
    def reload_templates(self):
        """Reload templates from file (useful for hot-reload)."""
        self._load_templates()
    
    async def render(
        self,
        notification_type: str,
        channel: str,
        title: str,
        message: str,
        data: Optional[Dict[str, Any]] = None
    ) -> Dict[str, str]:
        """
        Render notification content for a specific channel.
        
        Args:
            notification_type: Type of notification (e.g., "welcome", "security_alerts")
            channel: Delivery channel (in_app, email, sms, push)
            title: Raw notification title
            message: Raw notification message
            data: Additional variables for template substitution
            
        Returns:
            Dict with rendered content (keys depend on channel)
        """
        # Get template for this type, fallback to _default
        template = self.templates.get(notification_type, self.templates.get("_default", {}))
        channel_template = template.get(channel, {}) if isinstance(template, dict) else {}

        # If no template for this channel, use defaults
        if not channel_template:
            channel_template = self.templates.get("_default", {}).get(channel, {})

        # Normalize common alias keys from notification_templates.json
        channel_template = self._normalize_channel_template(channel, channel_template)
        
        # Build substitution variables
        variables = {
            "title": title,
            "message": message,
            "app_name": os.getenv("APP_NAME", "Mozaiks"),
            **(data or {})
        }
        
        # Render each field in the template
        rendered: Dict[str, str] = {}
        if isinstance(channel_template, dict):
            for key, template_str in channel_template.items():
                if template_str is None:
                    continue
                rendered[key] = self._substitute(str(template_str), variables)
        
        # Ensure required fields exist
        if channel == "email":
            rendered.setdefault("subject", title)
            rendered.setdefault("body_text", message)
        elif channel == "sms":
            rendered.setdefault("body_text", f"{title}: {message}")
        elif channel == "push":
            rendered.setdefault("title", title)
            rendered.setdefault("body", message)
        elif channel == "in_app":
            rendered.setdefault("title", title)
            rendered.setdefault("message", message)
        
        return rendered

    def _normalize_channel_template(self, channel: str, channel_template: Any) -> Dict[str, Any]:
        """Normalize common keys across template versions."""
        if not isinstance(channel_template, dict):
            return {}

        normalized: Dict[str, Any] = dict(channel_template)

        # Historical schema uses "body" for in-app content
        if channel == "in_app":
            if "message" not in normalized and "body" in normalized:
                normalized["message"] = normalized.get("body")

        # Historical schema uses "text_body" and "html_template" for email
        if channel == "email":
            if "body_text" not in normalized and "text_body" in normalized:
                normalized["body_text"] = normalized.get("text_body")
            # If someone supplied an html template filename, keep it as metadata but
            # do not require it for rendering.
            if "body_html" not in normalized and "html" in normalized:
                normalized["body_html"] = normalized.get("html")

        # Historical schema uses "body" for sms
        if channel == "sms":
            if "body_text" not in normalized and "body" in normalized:
                normalized["body_text"] = normalized.get("body")

        # Push commonly uses {title, body}; keep as-is.

        return normalized
    
    async def render_digest(
        self,
        digest_type: str,
        notifications: list,
        user_data: Optional[Dict[str, Any]] = None
    ) -> Dict[str, str]:
        """
        Render a digest email with multiple notifications.
        
        Args:
            digest_type: Type of digest (daily, weekly)
            notifications: List of notification dicts
            user_data: User information for personalization
            
        Returns:
            Dict with rendered email content
        """
        template = self.digest_templates.get(digest_type, {})
        
        # Build notification list HTML
        notification_html = ""
        notification_text = ""
        for n in notifications:
            notification_html += f"<li><strong>{n.get('title', '')}</strong>: {n.get('message', '')}</li>"
            notification_text += f"- {n.get('title', '')}: {n.get('message', '')}\n"
        
        variables = {
            "user_name": (user_data or {}).get("name", "User"),
            "notification_count": str(len(notifications)),
            "notification_list": notification_html,
            "period": "day" if digest_type == "daily" else "week",
            "app_name": os.getenv("APP_NAME", "Mozaiks"),
            **(user_data or {})
        }
        
        rendered = {}
        for key, template_str in template.items():
            rendered[key] = self._substitute(template_str, variables)
        
        # Fallback content
        if not rendered.get("subject"):
            rendered["subject"] = f"Your {variables['period']}ly notification digest"
        if not rendered.get("body_text"):
            rendered["body_text"] = f"You have {len(notifications)} notifications:\n{notification_text}"
        if not rendered.get("body_html"):
            rendered["body_html"] = f"<h2>Your Notifications</h2><ul>{notification_html}</ul>"
        
        return rendered
    
    def _substitute(self, template: str, variables: Dict[str, Any]) -> str:
        """
        Substitute {{variable}} placeholders in template string.
        
        Args:
            template: Template string with {{variable}} placeholders
            variables: Dict of variable values
            
        Returns:
            Rendered string
        """
        def replace(match):
            var_name = match.group(1).strip()
            return str(variables.get(var_name, match.group(0)))
        
        return re.sub(r"\{\{(\w+)\}\}", replace, template)
    
    def get_template_info(self, notification_type: str) -> Dict[str, Any]:
        """Get template definition for a notification type."""
        return self.templates.get(notification_type, self.templates.get("_default", {}))
    
    def list_templates(self) -> list:
        """List all available template types."""
        return list(self.templates.keys())


# Singleton instance
template_renderer = TemplateRenderer()
