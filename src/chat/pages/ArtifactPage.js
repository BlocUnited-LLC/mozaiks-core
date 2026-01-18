import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import Editor from '@monaco-editor/react';
import config from '../config';

const languageMap = {
  js: 'javascript',
  jsx: 'javascript',
  ts: 'typescript',
  tsx: 'typescript',
  py: 'python',
  json: 'json',
  css: 'css',
  html: 'html',
  md: 'markdown',
  yml: 'yaml',
  yaml: 'yaml',
  sh: 'shell',
};

const getLanguageFromPath = (filePath) => {
  if (!filePath || typeof filePath !== 'string') return 'plaintext';
  const ext = filePath.split('.').pop()?.toLowerCase();
  return languageMap[ext] || 'plaintext';
};

const buildTree = (paths) => {
  const root = {};
  for (const fullPath of paths) {
    const parts = String(fullPath).split('/').filter(Boolean);
    let cursor = root;
    for (let i = 0; i < parts.length; i += 1) {
      const part = parts[i];
      const isLeaf = i === parts.length - 1;
      if (!cursor[part]) {
        cursor[part] = isLeaf ? { __file: true, path: fullPath } : {};
      }
      cursor = cursor[part];
    }
  }
  return root;
};

const sortEntries = (entries) => {
  return [...entries].sort(([aName, aNode], [bName, bNode]) => {
    const aIsFile = Boolean(aNode && aNode.__file);
    const bIsFile = Boolean(bNode && bNode.__file);
    if (aIsFile !== bIsFile) return aIsFile ? 1 : -1;
    return String(aName).localeCompare(String(bName));
  });
};

const TreeNode = ({ name, node, depth, selectedPath, onSelectFile }) => {
  const isFile = Boolean(node && node.__file);
  const [open, setOpen] = useState(depth < 1);
  const isSelected = isFile && node.path === selectedPath;

  const toggle = () => {
    if (isFile) onSelectFile?.(node.path);
    else setOpen((v) => !v);
  };

  return (
    <div>
      <button
        type="button"
        onClick={toggle}
        className={[
          'w-full text-left rounded-md px-2 py-1 text-xs transition-colors',
          isSelected ? 'bg-white/10 text-white' : 'text-[var(--color-text-secondary)] hover:bg-white/5',
        ].join(' ')}
        style={{ paddingLeft: `${8 + depth * 14}px` }}
      >
        {name}
      </button>
      {!isFile && open && (
        <div>
          {sortEntries(Object.entries(node).filter(([k]) => k !== '__file')).map(([childName, childNode]) => (
            <TreeNode
              key={childName}
              name={childName}
              node={childNode}
              depth={depth + 1}
              selectedPath={selectedPath}
              onSelectFile={onSelectFile}
            />
          ))}
        </div>
      )}
    </div>
  );
};

const DEFAULT_ARTIFACT = {
  artifactId: 'demo',
  updatedAt: new Date().toISOString(),
  files: [
    {
      path: 'package.json',
      content: JSON.stringify(
        {
          name: 'artifact-demo',
          private: true,
          version: '0.0.0',
          type: 'module',
          scripts: {
            dev: 'vite --host 0.0.0.0 --port 3000',
          },
          dependencies: {
            react: '^18.0.0',
            'react-dom': '^18.0.0',
          },
          devDependencies: {
            vite: '^5.0.0',
            '@vitejs/plugin-react': '^4.2.0',
          },
        },
        null,
        2
      ),
    },
    {
      path: 'vite.config.js',
      content: `import { defineConfig } from 'vite'\nimport react from '@vitejs/plugin-react'\n\nexport default defineConfig({\n  plugins: [react()],\n  server: {\n    host: '0.0.0.0',\n    port: 3000,\n  },\n})\n`,
    },
    {
      path: 'index.html',
      content: `<!doctype html>\n<html>\n  <head>\n    <meta charset="UTF-8" />\n    <meta name="viewport" content="width=device-width, initial-scale=1.0" />\n    <title>Artifact Preview</title>\n  </head>\n  <body>\n    <div id="root"></div>\n    <script type="module" src="/src/main.jsx"></script>\n  </body>\n</html>\n`,
    },
    {
      path: 'src/main.jsx',
      content: `import React from 'react'\nimport ReactDOM from 'react-dom/client'\nimport App from './App.jsx'\n\nReactDOM.createRoot(document.getElementById('root')).render(\n  <React.StrictMode>\n    <App />\n  </React.StrictMode>,\n)\n`,
    },
    {
      path: 'src/App.jsx',
      content: `export default function App() {\n  return (\n    <div style={{ fontFamily: 'system-ui', padding: 24 }}>\n      <h1>Artifact Preview</h1>\n      <p>Edit files in the Code tab, then switch to Preview.</p>\n    </div>\n  )\n}\n`,
    },
  ],
};

const ArtifactPage = () => {
  const { artifactId } = useParams();
  const apiBaseUrl = config.get('api.baseUrl');
  const wsBaseUrl = config.get('api.wsUrl');

  const [tab, setTab] = useState('code');
  const [sandboxId, setSandboxId] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const [previewStatus, setPreviewStatus] = useState(null); // starting|running|error
  const [previewError, setPreviewError] = useState(null);
  const [wsDisconnected, setWsDisconnected] = useState(false);

  const [filesMap, setFilesMap] = useState(() => {
    const files = (DEFAULT_ARTIFACT.files || []).reduce((acc, f) => {
      acc[f.path] = f.content;
      return acc;
    }, {});
    return files;
  });

  const [selectedPath, setSelectedPath] = useState(() => {
    const keys = Object.keys(filesMap);
    return keys.includes('package.json') ? 'package.json' : keys[0] || null;
  });

  useEffect(() => {
    const keys = Object.keys(filesMap || {});
    setSelectedPath((prev) => (prev && filesMap?.[prev] != null ? prev : (keys.includes('package.json') ? 'package.json' : keys[0] || null)));
  }, [filesMap]);

  // Placeholder artifact loading: wire-ready for real backend storage.
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      if (!artifactId) return;
      try {
        const res = await fetch(`${apiBaseUrl}/api/artifacts/${encodeURIComponent(artifactId)}`);
        if (!res.ok) throw new Error('artifact not found');
        const data = await res.json();
        if (cancelled) return;
        const next = (data?.files || []).reduce((acc, f) => {
          if (f?.path && typeof f.content === 'string') acc[f.path] = f.content;
          return acc;
        }, {});
        if (Object.keys(next).length) setFilesMap(next);
      } catch {
        // Fallback to demo artifact
      }
    };
    load();
    return () => {
      cancelled = true;
    };
  }, [artifactId, apiBaseUrl]);

  const tree = useMemo(() => buildTree(Object.keys(filesMap || {})), [filesMap]);

  const currentContent = selectedPath ? filesMap?.[selectedPath] ?? '' : '';

  const lastSyncedRef = useRef({});
  const pendingDeletedRef = useRef(new Set());

  // Debounced sync (only when sandbox exists)
  useEffect(() => {
    if (!sandboxId) return;
    const t = setTimeout(async () => {
      const prev = lastSyncedRef.current || {};
      const changed = [];
      for (const [path, content] of Object.entries(filesMap || {})) {
        if (prev[path] !== content) changed.push({ path, content });
      }
      const deleted = Array.from(pendingDeletedRef.current || new Set());

      if (!changed.length && !deleted.length) return;

      try {
        const res = await fetch(`${apiBaseUrl}/api/sandbox/${encodeURIComponent(sandboxId)}/sync`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ files: changed, deleted }),
        });
        if (!res.ok) throw new Error(await res.text());
        lastSyncedRef.current = { ...(prev || {}), ...Object.fromEntries(changed.map((f) => [f.path, f.content])) };
        for (const p of deleted) {
          delete lastSyncedRef.current[p];
          pendingDeletedRef.current.delete(p);
        }
      } catch (e) {
        // Keep it minimal: surface error only in Preview
        setPreviewError(String(e));
      }
    }, 800);

    return () => clearTimeout(t);
  }, [filesMap, sandboxId, apiBaseUrl]);

  // WS connection for logs/status
  useEffect(() => {
    if (!sandboxId) return;

    setWsDisconnected(false);
    const wsUrl = `${wsBaseUrl.replace(/\/$/, '')}/ws/sandbox/${encodeURIComponent(sandboxId)}`;
    const ws = new WebSocket(wsUrl);

    ws.onclose = () => setWsDisconnected(true);
    ws.onerror = () => setWsDisconnected(true);
    ws.onmessage = (evt) => {
      try {
        const msg = JSON.parse(evt.data);
        if (msg?.type === 'status') {
          if (msg.status) setPreviewStatus(msg.status);
          if (msg.previewUrl) setPreviewUrl(msg.previewUrl);
          if (msg.error) setPreviewError(msg.error);
          if (msg.lastError) setPreviewError(msg.lastError);
        }
      } catch {
        // ignore
      }
    };

    // keepalive ping
    const ping = setInterval(() => {
      try {
        if (ws.readyState === WebSocket.OPEN) ws.send('ping');
      } catch {
        // ignore
      }
    }, 15000);

    return () => {
      clearInterval(ping);
      try {
        ws.close();
      } catch {
        // ignore
      }
    };
  }, [sandboxId, wsBaseUrl]);

  const openPreview = async () => {
    setPreviewError(null);
    setPreviewStatus('starting');

    try {
      let sid = sandboxId;
      if (!sid) {
        const res = await fetch(`${apiBaseUrl}/api/artifacts/${encodeURIComponent(artifactId || 'demo')}/sandbox`, {
          method: 'POST',
        });
        if (!res.ok) throw new Error(await res.text());
        const data = await res.json();
        sid = data.sandboxId;
        setSandboxId(sid);
      }

      const fullFiles = Object.entries(filesMap || {}).map(([path, content]) => ({ path, content }));
      const syncRes = await fetch(`${apiBaseUrl}/api/sandbox/${encodeURIComponent(sid)}/sync`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ files: fullFiles, deleted: [] }),
      });
      if (!syncRes.ok) throw new Error(await syncRes.text());
      lastSyncedRef.current = { ...(filesMap || {}) };

      const startRes = await fetch(`${apiBaseUrl}/api/sandbox/${encodeURIComponent(sid)}/start`, {
        method: 'POST',
      });
      if (!startRes.ok) throw new Error(await startRes.text());
      const started = await startRes.json();
      setPreviewStatus(started.status);
      setPreviewUrl(started.previewUrl || null);
      setPreviewError(started.message || null);
    } catch (e) {
      setPreviewStatus('error');
      setPreviewError(String(e));
    }
  };

  useEffect(() => {
    if (tab === 'preview') {
      openPreview();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab]);

  return (
    <div className="h-screen w-screen bg-[rgba(4,8,18,0.95)] text-white flex flex-col">
      <div className="flex items-center gap-2 border-b border-white/10 bg-black/40 px-4 py-2">
        <button
          type="button"
          className={[
            'px-3 py-1 rounded-md text-sm',
            tab === 'code' ? 'bg-white/10' : 'text-[var(--color-text-secondary)] hover:bg-white/5',
          ].join(' ')}
          onClick={() => setTab('code')}
        >
          Code
        </button>
        <button
          type="button"
          className={[
            'px-3 py-1 rounded-md text-sm',
            tab === 'preview' ? 'bg-white/10' : 'text-[var(--color-text-secondary)] hover:bg-white/5',
          ].join(' ')}
          onClick={() => setTab('preview')}
        >
          Preview
        </button>
      </div>

      {tab === 'code' && (
        <div className="flex flex-1 min-h-0">
          <div className="w-[280px] shrink-0 border-r border-white/10 bg-black/20 overflow-auto my-scroll1 p-2">
            {sortEntries(Object.entries(tree)).map(([name, node]) => (
              <TreeNode
                key={name}
                name={name}
                node={node}
                depth={0}
                selectedPath={selectedPath}
                onSelectFile={setSelectedPath}
              />
            ))}
          </div>
          <div className="flex-1 min-h-0">
            <Editor
              path={selectedPath || 'untitled'}
              language={getLanguageFromPath(selectedPath)}
              value={currentContent}
              theme="vs-dark"
              options={{
                minimap: { enabled: false },
                wordWrap: 'on',
                scrollBeyondLastLine: false,
                automaticLayout: true,
              }}
              onChange={(val) => {
                if (!selectedPath) return;
                const next = val ?? '';
                setFilesMap((prev) => ({ ...(prev || {}), [selectedPath]: next }));
              }}
            />
          </div>
        </div>
      )}

      {tab === 'preview' && (
        <div className="flex-1 min-h-0">
          <div className="h-full w-full bg-white relative">
            {!previewUrl && previewStatus === 'starting' && (
              <div className="absolute inset-0 flex items-center justify-center bg-black/30 text-sm text-white">
                Starting...
              </div>
            )}
            {previewStatus === 'error' && previewError && (
              <div className="absolute top-3 left-3 right-3 text-xs text-[var(--color-error)] bg-black/60 border border-white/10 rounded-md p-2">
                {previewError}
              </div>
            )}
            {wsDisconnected && (
              <div className="absolute bottom-2 left-2 text-[10px] text-[var(--color-text-muted)] bg-black/50 px-2 py-1 rounded">
                disconnected
              </div>
            )}
            {previewUrl && (
              <iframe
                title="Artifact Preview"
                src={previewUrl}
                className="w-full h-full border-0"
                sandbox="allow-scripts allow-same-origin allow-forms allow-popups"
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default ArtifactPage;
