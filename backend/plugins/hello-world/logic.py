"""
Hello World Plugin Logic

Demonstrates the basic structure of a MozaiksCore plugin.
"""

from typing import Dict, Any


class HelloWorldPlugin:
    """
    A simple hello world plugin demonstrating the plugin interface.
    
    Plugins in MozaiksCore extend the functionality of generated apps
    without modifying the core runtime.
    """
    
    def __init__(self, config: Dict[str, Any] = None):
        """
        Initialize the plugin with optional configuration.
        
        Args:
            config: Plugin-specific configuration dictionary
        """
        self.config = config or {}
        self.greeting = self.config.get("greeting", "Hello")
    
    def greet(self, name: str = "World") -> str:
        """
        Generate a greeting message.
        
        Args:
            name: Name to greet
            
        Returns:
            Greeting message string
        """
        return f"{self.greeting}, {name}!"
    
    def get_info(self) -> Dict[str, Any]:
        """
        Get plugin information.
        
        Returns:
            Dictionary with plugin metadata
        """
        return {
            "name": "hello-world",
            "version": "1.0.0",
            "description": "A simple example plugin",
            "author": "MozaiksCore Team"
        }
    
    # Plugin lifecycle methods
    
    def on_init(self, app_context: Any) -> None:
        """
        Called when the app initializes.
        
        Args:
            app_context: The application context object
        """
        print(f"HelloWorldPlugin initialized with greeting: {self.greeting}")
    
    def on_request(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Called on each request (if plugin is request-scoped).
        
        Args:
            request_data: Request metadata
            
        Returns:
            Modified request data or plugin response
        """
        return {
            "plugin": "hello-world",
            "message": self.greet(request_data.get("name", "User"))
        }
    
    def on_shutdown(self) -> None:
        """
        Called when the app shuts down.
        Cleanup resources here.
        """
        print("HelloWorldPlugin shutting down")


# Example usage
if __name__ == "__main__":
    # Create plugin instance
    plugin = HelloWorldPlugin(config={"greeting": "Hey"})
    
    # Test methods
    print(plugin.greet("Alice"))  # "Hey, Alice!"
    print(plugin.get_info())      # Plugin metadata
