import React, { useState } from 'react';

const AddTaskForm = ({ onAddTask }) => {
  const [task, setTask] = useState({
    title: '',
    description: '',
    priority: 'medium',
    due_date: ''
  });
  
  const handleChange = (e) => {
    const { name, value } = e.target;
    setTask(prev => ({ ...prev, [name]: value }));
  };
  
  const handleSubmit = (e) => {
    e.preventDefault();
    if (!task.title.trim()) return;
    
    onAddTask(task);
    
    // Reset form
    setTask({
      title: '',
      description: '',
      priority: 'medium',
      due_date: ''
    });
  };
  
  return (
    <form onSubmit={handleSubmit} className="bg-primary p-4 rounded-lg shadow">
      <div className="mb-4">
        <label className="block text-sm font-medium mb-1">Task Title</label>
        <input
          type="text"
          name="title"
          value={task.title}
          onChange={handleChange}
          className="w-full px-3 py-2 border border-gray-300 rounded"
          required
        />
      </div>
      
      <div className="mb-4">
        <label className="block text-sm font-medium mb-1">Description</label>
        <textarea
          name="description"
          value={task.description}
          onChange={handleChange}
          className="w-full px-3 py-2 border border-gray-300 rounded"
          rows="3"
        ></textarea>
      </div>
      
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
        <div>
          <label className="block text-sm font-medium mb-1">Priority</label>
          <select
            name="priority"
            value={task.priority}
            onChange={handleChange}
            className="w-full px-3 py-2 border border-gray-300 rounded"
          >
            <option value="low">Low</option>
            <option value="medium">Medium</option>
            <option value="high">High</option>
          </select>
        </div>
        
        <div>
          <label className="block text-sm font-medium mb-1">Due Date</label>
          <input
            type="date"
            name="due_date"
            value={task.due_date}
            onChange={handleChange}
            className="w-full px-3 py-2 border border-gray-300 rounded"
          />
        </div>
      </div>
      
      <button
        type="submit"
        className="bg-accent text-white px-4 py-2 rounded hover:bg-opacity-90 transition-colors"
      >
        Add Task
      </button>
    </form>
  );
};

export default AddTaskForm;