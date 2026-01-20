# backend/core/events/bus_base.py
import logging
import threading
import traceback
import asyncio
import time
from abc import ABC, abstractmethod

logger = logging.getLogger("mozaiks_core.event_bus")

class EventBusInterface(ABC):
    """
    Abstract base class for Event Bus implementations.
    MozaiksCore uses an in-process implementation.
    """
    @abstractmethod
    async def connect(self):
        pass

    @abstractmethod
    async def close(self):
        pass

    @abstractmethod
    def subscribe(self, event: str, callback):
        pass

    @abstractmethod
    def unsubscribe(self, event: str, callback):
        pass

    @abstractmethod
    def publish(self, event: str, data: dict):
        pass

class InMemoryEventBus(EventBusInterface):
    def __init__(self):
        self.subscribers = {}
        self.lock = threading.Lock()  # Ensures thread safety
        self.event_history = {}
        self.max_history_per_event = 100
        self.max_retry_count = 3
        self.async_queue = asyncio.Queue()
        self.is_processing = False
        self.task = None
        
        # Statistics for monitoring
        self.stats = {
            "events_published": 0,
            "events_delivered": 0,
            "delivery_failures": 0,
            "events_by_type": {}
        }

    async def connect(self):
        """No-op for in-memory bus"""
        pass

    async def close(self):
        """No-op for in-memory bus"""
        pass

    def subscribe(self, event, callback):
        """
        Subscribes a callback function to an event.
        """
        with self.lock:
            if event not in self.subscribers:
                self.subscribers[event] = []
            self.subscribers[event].append(callback)
            logger.info(f"✅ Subscribed '{callback.__name__}' to event '{event}'")

    def unsubscribe(self, event, callback):
        """
        Unsubscribes a callback from an event.
        """
        with self.lock:
            if event in self.subscribers and callback in self.subscribers[event]:
                self.subscribers[event].remove(callback)
                logger.info(f"❌ Unsubscribed '{callback.__name__}' from event '{event}'")

                # Remove event if no subscribers remain
                if not self.subscribers[event]:
                    del self.subscribers[event]

    def publish(self, event, data):
        """
        Publishes an event with the provided data.
        Uses fire-and-forget pattern for performance.
        """
        # Update statistics
        with self.lock:
            self.stats["events_published"] += 1
            if event not in self.stats["events_by_type"]:
                self.stats["events_by_type"][event] = 0
            self.stats["events_by_type"][event] += 1
            
            # Store in event history (with timestamp)
            if event not in self.event_history:
                self.event_history[event] = []
            
            # Add event to history with timestamp
            event_record = {
                "timestamp": time.time(),
                "data": data
            }
            
            # Maintain fixed history size
            history = self.event_history[event]
            history.append(event_record)
            if len(history) > self.max_history_per_event:
                history.pop(0)  # Remove oldest event
            
            # Get subscribers while holding the lock
            event_subscribers = self.subscribers.get(event, []).copy()
        
        if event_subscribers:
            logger.info(f"📢 Event '{event}' triggered with data: {data}")
            
            # Process subscribers outside the lock for better concurrency
            for callback in event_subscribers:
                try:
                    # Check if callback is a coroutine function and handle appropriately
                    if asyncio.iscoroutinefunction(callback):
                        # Queue the async task
                        asyncio.create_task(self._process_async_callback(callback, data, event))
                    else:
                        # Regular function, just call it
                        callback(data)
                        with self.lock:
                            self.stats["events_delivered"] += 1
                except Exception as e:
                    with self.lock:
                        self.stats["delivery_failures"] += 1
                    logger.error(f"❌ Error in event callback for '{event}': {e}")
                    logger.error(traceback.format_exc())
    
    async def _process_async_callback(self, callback, data, event_name, retry_count=0):
        """
        Process an async callback with retry logic
        """
        try:
            await callback(data)
            with self.lock:
                self.stats["events_delivered"] += 1
        except Exception as e:
            with self.lock:
                self.stats["delivery_failures"] += 1
            
            logger.error(f"❌ Error in async event callback for '{event_name}': {e}")
            logger.error(traceback.format_exc())
            
            # Retry logic for async callbacks
            if retry_count < self.max_retry_count:
                retry_delay = 0.5 * (2 ** retry_count)  # Exponential backoff
                logger.info(f"Retrying event callback for '{event_name}' in {retry_delay}s (attempt {retry_count + 1})")
                await asyncio.sleep(retry_delay)
                await self._process_async_callback(callback, data, event_name, retry_count + 1)
    
    def get_stats(self):
        """Return current event bus statistics"""
        with self.lock:
            return self.stats.copy()
    
    def reset_stats(self):
        """Reset statistics counters"""
        with self.lock:
            self.stats = {
                "events_published": 0,
                "events_delivered": 0,
                "delivery_failures": 0,
                "events_by_type": {}
            }
    
    async def start_background_processing(self):
        """Start background processing of async events if not already running"""
        if not self.is_processing:
            self.is_processing = True
            self.task = asyncio.create_task(self._process_queue())
            logger.info("Started background event processing")
    
    async def stop_background_processing(self):
        """Stop background processing of async events"""
        if self.is_processing and self.task:
            self.is_processing = False
            self.task.cancel()
            try:
                await self.task
            except asyncio.CancelledError:
                pass
            logger.info("Stopped background event processing")
    
    async def _process_queue(self):
        """Process async events from the queue"""
        while self.is_processing:
            try:
                event, data = await self.async_queue.get()
                await self._dispatch_event(event, data)
                self.async_queue.task_done()
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Error processing event queue: {e}")
                logger.error(traceback.format_exc())
    
    async def _dispatch_event(self, event, data):
        """Dispatch an event to subscribers"""
        with self.lock:
            subscribers = self.subscribers.get(event, []).copy()
        
        for callback in subscribers:
            try:
                if asyncio.iscoroutinefunction(callback):
                    await callback(data)
                else:
                    callback(data)
                    
                with self.lock:
                    self.stats["events_delivered"] += 1
            except Exception as e:
                with self.lock:
                    self.stats["delivery_failures"] += 1
                logger.error(f"Error in event callback: {e}")

    def get_event_history(self, event_type=None, limit=10):
        """
        Get event history for a specific event type or all events
        
        Args:
            event_type (str, optional): Specific event type to retrieve history for
            limit (int): Maximum number of events to return
            
        Returns:
            dict: Event history organized by event type
        """
        with self.lock:
            if event_type:
                # Return history for specific event type
                history = self.event_history.get(event_type, [])
                return {event_type: history[-limit:]}
            else:
                # Return history for all event types
                result = {}
                for evt, history in self.event_history.items():
                    result[evt] = history[-limit:]
                return result
