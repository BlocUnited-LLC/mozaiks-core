import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useChatUI } from '../context/ChatUIContext';
import { useWidgetMode } from '../hooks/useWidgetMode';
import Header from '../components/layout/Header';

/**
 * My Workflows Page
 *
 * Displays all created workflows/apps with search, filtering, and quick actions.
 * Users can view, resume, export, or delete their generated applications.
 */
const MyWorkflowsPage = () => {
  const navigate = useNavigate();
  const {
    config
  } = useChatUI();
  useWidgetMode(); // Enable persistent chat widget for this page
  const [workflows, setWorkflows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [filterStatus, setFilterStatus] = useState('all'); // all, completed, in-progress
  const [sortBy, setSortBy] = useState('recent'); // recent, name, status

  // App ID for API calls (currently using mock data)
  // eslint-disable-next-line no-unused-vars
  const currentAppId = config?.chat?.defaultAppId || process.env.REACT_APP_DEFAULT_APP_ID;

  useEffect(() => {
    loadWorkflows();
  }, []);

  const loadWorkflows = async () => {
    setLoading(true);
    try {
      // TODO: Replace with actual API call to backend
      // const response = await fetch(`/api/workflows/${currentAppId}`);
      // const data = await response.json();
      
      // Mock data for now
      const mockWorkflows = [
        {
          id: 'wf-001',
          name: 'Revenue Dashboard',
          description: 'Analytics dashboard with real-time metrics',
          status: 'completed',
          created_at: new Date('2025-01-10'),
          updated_at: new Date('2025-01-15'),
          chat_id: 'chat-001',
          workflow_type: 'MozaiksAI',
          thumbnail: null,
          tags: ['analytics', 'dashboard']
        },
        {
          id: 'wf-002',
          name: 'User Management System',
          description: 'CRUD operations for user accounts',
          status: 'in-progress',
          created_at: new Date('2025-01-14'),
          updated_at: new Date('2025-01-15'),
          chat_id: 'chat-002',
          workflow_type: 'MozaiksAI',
          thumbnail: null,
          tags: ['auth', 'admin']
        },
      ];
      
      setWorkflows(mockWorkflows);
    } catch (error) {
      console.error('Failed to load workflows:', error);
    } finally {
      setLoading(false);
    }
  };

  const filteredWorkflows = workflows
    .filter(wf => {
      // Status filter
      if (filterStatus !== 'all' && wf.status !== filterStatus) return false;
      
      // Search filter
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        return (
          wf.name.toLowerCase().includes(query) ||
          wf.description?.toLowerCase().includes(query) ||
          wf.tags?.some(tag => tag.toLowerCase().includes(query))
        );
      }
      
      return true;
    })
    .sort((a, b) => {
      switch (sortBy) {
        case 'name':
          return a.name.localeCompare(b.name);
        case 'status':
          return a.status.localeCompare(b.status);
        case 'recent':
        default:
          return new Date(b.updated_at) - new Date(a.updated_at);
      }
    });

  const handleResumeWorkflow = (workflow) => {
    // Navigate back to chat with the specific chat_id
    navigate(`/chat?chat_id=${workflow.chat_id}&workflow=${workflow.workflow_type}`);
  };

  const handleViewArtifact = (workflow) => {
    // Open chat with artifact panel visible
    navigate(`/chat?chat_id=${workflow.chat_id}&workflow=${workflow.workflow_type}&view=artifact`);
  };

  const handleExportWorkflow = async (workflow) => {
    try {
      // TODO: Implement export functionality
      console.log('Exporting workflow:', workflow.id);
      // Could download as JSON, zip with code, or generate deployment package
    } catch (error) {
      console.error('Export failed:', error);
    }
  };

  const handleDeleteWorkflow = async (workflow) => {
    if (!window.confirm(`Are you sure you want to delete "${workflow.name}"?`)) {
      return;
    }
    
    try {
      // TODO: API call to delete workflow
      setWorkflows(prev => prev.filter(wf => wf.id !== workflow.id));
    } catch (error) {
      console.error('Delete failed:', error);
    }
  };

  const getStatusBadge = (status) => {
    const styles = {
      completed: 'bg-green-500/20 text-green-400 border-green-500/30',
      'in-progress': 'bg-blue-500/20 text-blue-400 border-blue-500/30',
      failed: 'bg-red-500/20 text-red-400 border-red-500/30'
    };
    
    return (
      <span className={`px-2 py-1 text-xs rounded-full border ${styles[status] || styles['in-progress']}`}>
        {status === 'in-progress' ? 'In Progress' : status.charAt(0).toUpperCase() + status.slice(1)}
      </span>
    );
  };

  return (
    <div className="flex flex-col h-screen bg-gradient-to-br from-gray-900 via-gray-800 to-black">
      <Header onDiscoverClick={() => navigate('/chat')} />
      
      <main className="flex-1 overflow-hidden flex flex-col pt-16 sm:pt-20 md:pt-16">
        {/* Page Header */}
        <div className="flex-shrink-0 px-6 py-6 border-b border-gray-700/50 bg-gradient-to-r from-[rgba(var(--color-primary-rgb),0.05)] to-[rgba(var(--color-secondary-rgb),0.05)] backdrop-blur-xl">
          <div className="max-w-7xl mx-auto">
            <div className="flex items-center justify-between mb-6">
              <div>
                <h1 className="text-3xl font-bold text-white mb-2 flex items-center gap-3">
                  <span className="text-[var(--color-primary-light)]">📁</span>
                  My Workflows
                </h1>
                <p className="text-gray-400">Manage your AI-generated applications and workflows</p>
              </div>
              <button
                onClick={() => navigate('/chat')}
                className="px-6 py-3 bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-secondary)] text-white rounded-xl hover:shadow-lg hover:shadow-[var(--color-primary)]/50 transition-all duration-300 font-semibold flex items-center justify-center md:px-6 md:py-3 sm:w-12 sm:h-12 sm:p-0"
                title="Create New Workflow"
              >
                <span className="hidden md:inline">+ Create New Workflow</span>
                <span className="md:hidden text-2xl leading-none">+</span>
              </button>
            </div>
            
            {/* Search and Filters */}
            <div className="flex flex-wrap gap-4 items-center">
              {/* Search */}
              <div className="flex-1 min-w-[300px]">
                <input
                  type="text"
                  placeholder="Search workflows, tags, or descriptions..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="w-full px-4 py-2 bg-gray-800/50 border border-gray-700 rounded-lg text-white placeholder-gray-500 focus:border-[var(--color-primary-light)] focus:ring-2 focus:ring-[var(--color-primary-light)]/20 transition-all"
                />
              </div>
              
              {/* Status Filter */}
              <select
                value={filterStatus}
                onChange={(e) => setFilterStatus(e.target.value)}
                className="px-4 py-2 bg-gray-800/50 border border-gray-700 rounded-lg text-white focus:border-[var(--color-primary-light)] focus:ring-2 focus:ring-[var(--color-primary-light)]/20 transition-all"
              >
                <option value="all">All Status</option>
                <option value="completed">Completed</option>
                <option value="in-progress">In Progress</option>
              </select>
              
              {/* Sort */}
              <select
                value={sortBy}
                onChange={(e) => setSortBy(e.target.value)}
                className="px-4 py-2 bg-gray-800/50 border border-gray-700 rounded-lg text-white focus:border-[var(--color-primary-light)] focus:ring-2 focus:ring-[var(--color-primary-light)]/20 transition-all"
              >
                <option value="recent">Recently Updated</option>
                <option value="name">Name (A-Z)</option>
                <option value="status">Status</option>
              </select>
            </div>
          </div>
        </div>
        
        {/* Workflows Grid */}
        <div className="flex-1 overflow-y-auto p-6">
          <div className="max-w-7xl mx-auto">
            {loading ? (
              <div className="flex items-center justify-center h-64">
                <div className="text-gray-400">Loading workflows...</div>
              </div>
            ) : filteredWorkflows.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-64 text-center">
                <div className="text-6xl mb-4">📦</div>
                <h3 className="text-xl font-semibold text-white mb-2">
                  {searchQuery ? 'No workflows found' : 'No workflows yet'}
                </h3>
                <p className="text-gray-400 mb-6">
                  {searchQuery 
                    ? 'Try a different search term'
                    : 'Start creating your first AI-powered application'}
                </p>
                {!searchQuery && (
                  <button
                    onClick={() => navigate('/chat')}
                    className="px-6 py-3 bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-secondary)] text-white rounded-xl hover:shadow-lg transition-all duration-300"
                  >
                    Create Your First Workflow
                  </button>
                )}
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {filteredWorkflows.map(workflow => (
                  <div
                    key={workflow.id}
                    className="group bg-gradient-to-br from-gray-800/50 to-gray-900/50 border border-gray-700/50 rounded-xl p-6 hover:border-[var(--color-primary-light)]/50 hover:shadow-lg hover:shadow-[var(--color-primary)]/20 transition-all duration-300 cursor-pointer"
                    onClick={() => handleResumeWorkflow(workflow)}
                  >
                    {/* Workflow Header */}
                    <div className="flex items-start justify-between mb-4">
                      <div className="flex-1">
                        <h3 className="text-lg font-semibold text-white mb-2 group-hover:text-[var(--color-primary-light)] transition-colors">
                          {workflow.name}
                        </h3>
                        <p className="text-sm text-gray-400 line-clamp-2">
                          {workflow.description || 'No description'}
                        </p>
                      </div>
                      {getStatusBadge(workflow.status)}
                    </div>
                    
                    {/* Tags */}
                    {workflow.tags && workflow.tags.length > 0 && (
                      <div className="flex flex-wrap gap-2 mb-4">
                        {workflow.tags.map(tag => (
                          <span
                            key={tag}
                            className="px-2 py-1 text-xs bg-gray-700/50 text-gray-300 rounded-md"
                          >
                            #{tag}
                          </span>
                        ))}
                      </div>
                    )}
                    
                    {/* Metadata */}
                    <div className="text-xs text-gray-500 mb-4 space-y-1">
                      <div>Created: {new Date(workflow.created_at).toLocaleDateString()}</div>
                      <div>Updated: {new Date(workflow.updated_at).toLocaleDateString()}</div>
                    </div>
                    
                    {/* Actions */}
                    <div className="flex gap-2">
                      <button
                        onClick={(e) => { e.stopPropagation(); handleResumeWorkflow(workflow); }}
                        className="flex-1 px-3 py-2 bg-[var(--color-primary)]/20 text-[var(--color-primary-light)] rounded-lg hover:bg-[var(--color-primary)]/30 transition-all text-sm font-medium"
                      >
                        Resume
                      </button>
                      <button
                        onClick={(e) => { e.stopPropagation(); handleViewArtifact(workflow); }}
                        className="flex-1 px-3 py-2 bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-all text-sm font-medium"
                        title="View Artifact"
                      >
                        View
                      </button>
                      <button
                        onClick={(e) => { e.stopPropagation(); handleExportWorkflow(workflow); }}
                        className="px-3 py-2 bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-all"
                        title="Export"
                      >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                        </svg>
                      </button>
                      <button
                        onClick={(e) => { e.stopPropagation(); handleDeleteWorkflow(workflow); }}
                        className="px-3 py-2 bg-red-500/20 text-red-400 rounded-lg hover:bg-red-500/30 transition-all"
                        title="Delete"
                      >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
};

export default MyWorkflowsPage;
