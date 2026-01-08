// src/plugins/notes_manager/components/NotesPage.jsx
import React, { useState, useEffect } from 'react';
import { useAuth } from '../../../auth/AuthContext';
import NotesList from './NotesList';
import NoteEditor from './NoteEditor';
import NoteToolbar from './NoteToolbar';

const NotesPage = () => {
  const { authFetch } = useAuth();
  const [notes, setNotes] = useState([]);
  const [selectedNote, setSelectedNote] = useState(null);
  const [isEditing, setIsEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [categories, setCategories] = useState(['general', 'work', 'personal']);
  
  // Load notes on component mount
  useEffect(() => {
    fetchNotes();
  }, []);
  
  const fetchNotes = async () => {
    setLoading(true);
    try {
      const response = await authFetch('/api/execute/notes_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'get_notes'
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to fetch notes');
      }
      
      const data = await response.json();
      setNotes(data.notes || []);
      
      // Extract unique categories
      if (data.notes && data.notes.length > 0) {
        const uniqueCategories = [...new Set(data.notes.map(note => note.category))];
        setCategories(['general', ...uniqueCategories.filter(c => c !== 'general')]);
      }
    } catch (err) {
      console.error('Error fetching notes:', err);
      setError('Failed to load notes. Please try again later.');
    } finally {
      setLoading(false);
    }
  };
  
  const addNote = async (noteData) => {
    try {
      const response = await authFetch('/api/execute/notes_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'add_note',
          note: noteData
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to add note');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh notes
        fetchNotes();
        // Select the new note
        setSelectedNote(data.note);
        setIsEditing(true);
      }
    } catch (err) {
      console.error('Error adding note:', err);
      setError('Failed to add note. Please try again.');
    }
  };
  
  const updateNote = async (noteData) => {
    try {
      const response = await authFetch('/api/execute/notes_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'update_note',
          note: noteData
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to update note');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh notes
        fetchNotes();
        // Update selected note
        setSelectedNote(data.note);
      }
    } catch (err) {
      console.error('Error updating note:', err);
      setError('Failed to update note. Please try again.');
    }
  };
  
  const deleteNote = async (noteId) => {
    try {
      const response = await authFetch('/api/execute/notes_manager', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          action: 'delete_note',
          note_id: noteId
        })
      });
      
      if (!response.ok) {
        throw new Error('Failed to delete note');
      }
      
      const data = await response.json();
      if (data.success) {
        // Refresh notes
        fetchNotes();
        // Clear selection if the deleted note was selected
        if (selectedNote && selectedNote.id === noteId) {
          setSelectedNote(null);
          setIsEditing(false);
        }
      }
    } catch (err) {
      console.error('Error deleting note:', err);
      setError('Failed to delete note. Please try again.');
    }
  };
  
  const handleNoteSelect = (note) => {
    setSelectedNote(note);
    setIsEditing(true);
  };
  
  const handleNewNote = () => {
    // Create an empty note template
    const newNoteTemplate = {
      title: "",
      content: "",
      category: "general",
      color: "#ffffff",
      pinned: false
    };
    
    // Start with a new note
    setSelectedNote(newNoteTemplate);
    setIsEditing(true);
  };
  
  const handleNoteChange = (field, value) => {
    setSelectedNote(prev => ({
      ...prev,
      [field]: value
    }));
  };
  
  const handleSaveNote = () => {
    if (!selectedNote) return;
    
    // Validate required fields
    if (!selectedNote.title.trim()) {
      setError('Note title is required');
      return;
    }
    
    // If the note has an ID, update it, otherwise add it
    if (selectedNote.id) {
      updateNote(selectedNote);
    } else {
      addNote(selectedNote);
    }
  };
  
  const handleCancelEdit = () => {
    // If it's a new note, clear selection
    if (!selectedNote.id) {
      setSelectedNote(null);
    }
    setIsEditing(false);
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
      <div className="mb-6">
        <NoteToolbar 
          onNewNote={handleNewNote} 
          categories={categories}
        />
      </div>
      
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
          <p>{error}</p>
          <button 
            className="text-sm underline ml-2" 
            onClick={() => setError(null)}
          >
            Dismiss
          </button>
        </div>
      )}
      
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="md:col-span-1">
          <h2 className="text-xl font-semibold mb-4">Your Notes</h2>
          <NotesList 
            notes={notes} 
            onSelectNote={handleNoteSelect}
            onDeleteNote={deleteNote}
            selectedNoteId={selectedNote?.id}
          />
        </div>
        
        <div className="md:col-span-2">
          <h2 className="text-xl font-semibold mb-4">
            {isEditing 
              ? (selectedNote?.id ? "Edit Note" : "New Note") 
              : (selectedNote ? "View Note" : "Select a note to view")}
          </h2>
          
          {selectedNote ? (
            <NoteEditor 
              note={selectedNote}
              isEditing={isEditing}
              onNoteChange={handleNoteChange}
              onSave={handleSaveNote}
              onCancel={handleCancelEdit}
              onEdit={() => setIsEditing(true)}
              categories={categories}
            />
          ) : (
            <div className="bg-primary p-4 rounded-lg shadow text-center">
              <p className="text-gray-500">Select a note from the list or create a new one</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default NotesPage;
