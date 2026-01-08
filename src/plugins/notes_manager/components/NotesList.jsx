
// /src/plugins/notes_manager/components/NoteList.jsx
import React from 'react';

const NotesList = ({ notes, onSelectNote, onDeleteNote, selectedNoteId }) => {
  // Format date to a readable format
  const formatDate = (dateString) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString();
  };
  
  // Sort notes: pinned first, then by updated_at
  const sortedNotes = [...notes].sort((a, b) => {
    // Pinned notes go to the top
    if (a.pinned !== b.pinned) {
      return a.pinned ? -1 : 1;
    }
    
    // Sort by updated_at (newest first)
    return new Date(b.updated_at) - new Date(a.updated_at);
  });
  
  if (notes.length === 0) {
    return (
      <div className="bg-primary p-4 rounded-lg shadow text-center">
        <p className="text-gray-500">No notes yet. Create your first note!</p>
      </div>
    );
  }
  
  return (
    <div className="bg-primary rounded-lg shadow overflow-hidden">
      <ul className="divide-y divide-gray-200">
        {sortedNotes.map(note => (
          <li 
            key={note.id}
            className={`relative hover:bg-secondary transition-colors ${
              note.id === selectedNoteId ? 'bg-secondary' : ''
            }`}
          >
            {note.pinned && (
              <div className="absolute top-0 left-0 p-1">
                <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4 text-accent" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M5 5a2 2 0 012-2h10a2 2 0 012 2v10a2 2 0 01-2 2H7a2 2 0 01-2-2V5z" />
                </svg>
              </div>
            )}
            
            <div 
              className="p-4 cursor-pointer"
              onClick={() => onSelectNote(note)}
            >
              <div className="flex justify-between items-start">
                <div className="flex-1">
                  <h3 className="font-medium text-lg">{note.title}</h3>
                  <p className="text-xs text-gray-500 mt-1">
                    {formatDate(note.updated_at)}
                    {note.category && (
                      <span className="ml-2 inline-block px-2 py-0.5 bg-secondary rounded-full">
                        {note.category}
                      </span>
                    )}
                  </p>
                  {note.content && (
                    <p className="text-sm text-gray-600 mt-2 truncate">
                      {note.content.slice(0, 100)}
                    </p>
                  )}
                </div>
                
                <button
                  onClick={(e) => {
                    e.stopPropagation(); // Prevent triggering the note selection
                    onDeleteNote(note.id);
                  }}
                  className="text-red-500 hover:text-red-700 ml-2"
                  title="Delete note"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z" clipRule="evenodd" />
                  </svg>
                </button>
              </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default NotesList;