// /src/plugins/notes_manager/components/NoteEditor.jsx
import React from 'react';

const NoteEditor = ({ note, isEditing, onNoteChange, onSave, onCancel, onEdit, categories }) => {
  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    onNoteChange(name, type === 'checkbox' ? checked : value);
  };
  
  return (
    <div className="bg-primary p-4 rounded-lg shadow">
      {isEditing ? (
        <form onSubmit={(e) => {
          e.preventDefault();
          onSave();
        }}>
          <div className="mb-4">
            <label className="block text-sm font-medium mb-1">Title</label>
            <input
              type="text"
              name="title"
              value={note.title}
              onChange={handleChange}
              className="w-full px-3 py-2 border border-gray-300 rounded"
              required
            />
          </div>
          
          <div className="mb-4">
            <label className="block text-sm font-medium mb-1">Content</label>
            <textarea
              name="content"
              value={note.content}
              onChange={handleChange}
              className="w-full px-3 py-2 border border-gray-300 rounded"
              rows="8"
            ></textarea>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
            <div>
              <label className="block text-sm font-medium mb-1">Category</label>
              <select
                name="category"
                value={note.category}
                onChange={handleChange}
                className="w-full px-3 py-2 border border-gray-300 rounded"
              >
                {categories.map(category => (
                  <option key={category} value={category}>
                    {category.charAt(0).toUpperCase() + category.slice(1)}
                  </option>
                ))}
              </select>
            </div>
            
            <div>
              <label className="block text-sm font-medium mb-1">Color</label>
              <input
                type="color"
                name="color"
                value={note.color}
                onChange={handleChange}
                className="w-full h-10 px-3 py-2 border border-gray-300 rounded"
              />
            </div>
            
            <div className="flex items-center">
              <input
                type="checkbox"
                name="pinned"
                id="pinned"
                checked={note.pinned}
                onChange={handleChange}
                className="h-4 w-4 text-accent rounded focus:ring-accent"
              />
              <label htmlFor="pinned" className="ml-2 text-sm font-medium">
                Pin this note
              </label>
            </div>
          </div>
          
          <div className="flex justify-end space-x-3">
            <button
              type="button"
              onClick={onCancel}
              className="px-4 py-2 border border-gray-300 rounded hover:bg-gray-100"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-accent text-white rounded hover:bg-opacity-90"
            >
              Save
            </button>
          </div>
        </form>
      ) : (
        <div>
          <div 
            className="p-4 rounded mb-4"
            style={{ backgroundColor: note.color + '33' }}  // Add transparency
          >
            <div className="flex justify-between items-start">
              <h3 className="text-xl font-bold">{note.title}</h3>
              {note.pinned && (
                <span className="bg-accent text-white px-2 py-0.5 rounded-full text-xs">
                  Pinned
                </span>
              )}
            </div>
            
            <div className="mt-2">
              <span className="inline-block px-2 py-0.5 bg-secondary rounded-full text-xs">
                {note.category}
              </span>
            </div>
            
            <div className="mt-4 whitespace-pre-wrap">
              {note.content}
            </div>
          </div>
          
          <div className="flex justify-end">
            <button
              onClick={onEdit}
              className="px-4 py-2 bg-accent text-white rounded hover:bg-opacity-90"
            >
              Edit
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default NoteEditor;