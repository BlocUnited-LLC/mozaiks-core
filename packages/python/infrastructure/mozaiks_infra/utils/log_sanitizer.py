# runtime/ai/core/utils/log_sanitizer.py
"""
Log sanitization utilities to prevent log injection attacks.

Log injection occurs when user-controlled data containing newlines (\r, \n)
is written to logs, allowing attackers to forge fake log entries.
"""


def sanitize_for_log(value) -> str:
    """
    Sanitize a value for safe inclusion in log messages.
    
    Removes newline characters that could be used to forge log entries.
    
    Args:
        value: Any value to sanitize (will be converted to string)
        
    Returns:
        Sanitized string safe for logging
        
    Example:
        >>> sanitize_for_log("user\\nAdmin: logged in")
        'user Admin: logged in'
    """
    if value is None:
        return ""
    text = str(value)
    return text.replace("\r\n", " ").replace("\n", " ").replace("\r", " ")


def sanitize_dict_for_log(d: dict) -> str:
    """
    Sanitize a dictionary for safe logging.
    
    Converts dict to string and removes dangerous characters.
    """
    if d is None:
        return "{}"
    return sanitize_for_log(str(d))
