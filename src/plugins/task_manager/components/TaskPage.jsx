// src/plugins/task_manager/components/TaskPage.jsx
import React, { useState, useEffect } from 'react';
import { useAuth } from '../../../auth/AuthContext';
import TaskList from './TaskList';
import AddTaskForm from './AddTaskForm';

const TaskPage = () => {
  const { authFetch } = useAuth();
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  
  // Load tasks on component mount
  useEffect(() => {
    fetchTasks();
  }, []);
  
  const fetchTasks = async () => {
    setLoading(true);
    try {
      const response = await authFetch('/api/execute/task_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'get_tasks'
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch tasks');
      }
      
      const data = await response.json();
      setTasks(data.tasks || []);
    } catch (err) {
      console.error('Error fetching tasks:', err);
      setError('Failed to load tasks. Please try again later.');
    } finally {
      setLoading(false);
    }
  };
  
  const addTask = async (taskData) => {
    try {
      const response = await authFetch('/api/execute/task_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'add_task',
          task: taskData
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to add task');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh tasks
        fetchTasks();
      }
    } catch (err) {
      console.error('Error adding task:', err);
      setError('Failed to add task. Please try again.');
    }
  };
  
  const completeTask = async (taskId) => {
    try {
      const response = await authFetch('/api/execute/task_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'complete_task',
          task_id: taskId
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to complete task');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh tasks
        fetchTasks();
      }
    } catch (err) {
      console.error('Error completing task:', err);
      setError('Failed to complete task. Please try again.');
    }
  };
  
  const deleteTask = async (taskId) => {
    try {
      const response = await authFetch('/api/execute/task_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'delete_task',
          task_id: taskId
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to delete task');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh tasks
        fetchTasks();
      }
    } catch (err) {
      console.error('Error deleting task:', err);
      setError('Failed to delete task. Please try again.');
    }
  };
  
  if (loading) {
    return (
      <div className="flex justify-center items-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-accent"></div>
      </div>
    );
  }
  
  return (
    <div className="p-4">
      <h1 className="text-2xl font-bold mb-6">Task Manager</h1>
      
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
          <p>{error}</p>
        </div>
      )}
      
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <h2 className="text-xl font-semibold mb-4">Add New Task</h2>
          <AddTaskForm onAddTask={addTask} />
        </div>
        
        <div>
          <h2 className="text-xl font-semibold mb-4">Your Tasks</h2>
          <TaskList 
            tasks={tasks} 
            onCompleteTask={completeTask} 
            onDeleteTask={deleteTask}
          />
        </div>
      </div>
    </div>
  );
};

export default TaskPage;
