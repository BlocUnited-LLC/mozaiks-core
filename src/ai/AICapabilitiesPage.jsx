import React, { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext';

const interpolateTemplate = (template, values) => {
  return (template || '').replace(/{([^}]+)}/g, (match, key) => {
    if (!(key in values)) return match;
    const value = values[key];
    if (value === undefined || value === null) return '';
    if (key === 'runtime_ui_base_url') return String(value);
    return encodeURIComponent(String(value));
  });
};

const CapabilityCard = ({ capability, onLaunch, launching }) => {
  return (
    <div className="p-4 bg-secondary rounded-lg border border-gray-700 flex flex-col gap-2">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="text-lg font-semibold text-text-primary">{capability.display_name || capability.id}</h3>
          {capability.description ? <p className="text-sm text-text-secondary mt-1">{capability.description}</p> : null}
        </div>
      </div>

      <div className="mt-2 flex items-center justify-end">
        <button
          type="button"
          className="px-4 py-2 bg-accent text-white rounded hover:bg-opacity-90 disabled:opacity-50"
          disabled={launching}
          onClick={() => onLaunch(capability.id)}
        >
          {launching ? 'Launching…' : 'Launch'}
        </button>
      </div>
    </div>
  );
};

const AICapabilitiesPage = () => {
  const { authFetch } = useAuth();

  const [capabilities, setCapabilities] = useState([]);
  const [plan, setPlan] = useState('free');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [launchingId, setLaunchingId] = useState(null);

  const loadCapabilities = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await authFetch('/api/ai/capabilities');
      const data = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(data?.detail || 'Failed to load AI capabilities');

      setCapabilities(Array.isArray(data.capabilities) ? data.capabilities : []);
      setPlan((data.plan || 'free').toString());
    } catch (err) {
      setError(err?.message || 'Failed to load AI capabilities');
    } finally {
      setLoading(false);
    }
  }, [authFetch]);

  useEffect(() => {
    loadCapabilities();
  }, [loadCapabilities]);

  const launch = useCallback(
    async (capabilityId) => {
      setLaunchingId(capabilityId);
      setError(null);
      try {
        const response = await authFetch('/api/ai/launch', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ capability_id: capabilityId }),
        });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data?.detail || 'Launch failed');

        const token = data.launch_token;
        if (!token) throw new Error('Launch token is missing (backend did not return launch_token)');

        const runtimeUiBaseUrl = data?.runtime?.runtime_ui_base_url;
        const template = data?.runtime?.chatui_url_template;
        if (!runtimeUiBaseUrl) throw new Error('Runtime UI is not configured (missing runtime_ui_base_url)');
        if (!template) throw new Error('Runtime UI is not configured (missing chatui_url_template)');

        const url = interpolateTemplate(template, {
          runtime_ui_base_url: runtimeUiBaseUrl,
          app_id: data.app_id,
          capability_id: data.capability_id,
          chat_id: data.chat_id,
          token,
        });

        window.open(url, '_blank', 'noopener,noreferrer');
      } catch (err) {
        setError(err?.message || 'Launch failed');
      } finally {
        setLaunchingId(null);
      }
    },
    [authFetch]
  );

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-text-primary">AI</h1>
          <p className="text-text-secondary mt-1">
            Launch chat-native workflows in the external MozaiksAI runtime. Plan: <span className="font-medium">{plan}</span>
          </p>
        </div>
        <button
          type="button"
          className="px-3 py-2 rounded bg-secondary text-text-primary hover:bg-opacity-90"
          onClick={loadCapabilities}
          disabled={loading}
        >
          Refresh
        </button>
      </div>

      {error ? <div className="p-3 rounded bg-red-900/30 text-red-200 border border-red-800">{error}</div> : null}

      {loading ? (
        <div className="p-4 text-text-secondary">Loading capabilities…</div>
      ) : capabilities.length === 0 ? (
        <div className="p-4 text-text-secondary">No capabilities available.</div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {capabilities.map((cap) => (
            <CapabilityCard
              key={cap.id}
              capability={cap}
              onLaunch={launch}
              launching={launchingId === cap.id}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default AICapabilitiesPage;
