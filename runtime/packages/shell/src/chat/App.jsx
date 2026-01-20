import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { ChatUIProvider } from './context/ChatUIContext';
import ChatPage from './pages/ChatPage';
import MyWorkflowsPage from './pages/MyWorkflowsPage';
import ArtifactPage from './pages/ArtifactPage';
import './styles/TransportAwareChat.css';

/**
 * AppContent - Inner component that has access to context and location
 */
const AppContent = () => {
  return (
    <Routes>
      <Route path="/artifacts/:artifactId" element={<ArtifactPage />} />
      <Route path="/workflows" element={<MyWorkflowsPage />} />
      <Route path="/my-workflows" element={<MyWorkflowsPage />} />
      <Route path="*" element={<ChatPage />} />
    </Routes>
  );
};

// Unified ChatUI App - Transport-agnostic chat with Simple Events integration
function App() {
  const handleChatUIReady = () => {
    console.log('ChatUI is ready!');
  };

  return (
    <ChatUIProvider onReady={handleChatUIReady}>
      <Router future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <AppContent />
      </Router>
    </ChatUIProvider>
  );
}

export default App;
