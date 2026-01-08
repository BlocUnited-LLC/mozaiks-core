// /src/plugins/notes_manager/components/NoteToolbar.jsx
import React, { useState } from 'react';

const NoteToolbar = ({ onNewNote, categories }) => {
  const [selectedCategory, setSelectedCategory] = useState('all');
  
  return (
    <div className="bg-primary p-4 rounded-lg shadow flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
      <div>
        <button
          onClick={onNewNote}
          className="bg-accent text-white px-4 py-2 rounded flex items-center hover:bg-opacity-90"
        >
          <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5 mr-2" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
          </svg>
          New Note
        </button>
      </div>
      
      <div className="flex items-center">
        <label className="text-sm font-medium mr-2">Filter:</label>
        <select
          value={selectedCategory}
          onChange={(e) => setSelectedCategory(e.target.value)}
          className="px-3 py-2 border border-gray-300 rounded focus:ring-accent focus:border-accent"
        >
          <option value="all">All Categories</option>
          {categories.map(category => (
            <option key={category} value={category}>
              {category.charAt(0).toUpperCase() + category.slice(1)}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
};

export default NoteToolbar;