import React from 'react';
import UIToolRenderer from '../../core/ui/UIToolRenderer';

const ArtifactPanel = ({ onClose, isMobile = false, isEmbedded = false, messages = [], workflowName = null }) => {
  const showMobileClose = Boolean(isMobile && onClose && !isEmbedded);
  const isMobileEmbedded = Boolean(isMobile && isEmbedded);

  const containerClasses = isMobileEmbedded
    ? 'flex flex-col w-full h-full min-h-0'
    : (isMobile
        ? 'fixed inset-0 z-50 min-h-0'
        : 'flex flex-col w-full h-full min-h-0 self-stretch transition-all duration-500 ease-in-out');

  // Match ChatInterface styling exactly for consistent neon border effect
  const contentClasses = isMobileEmbedded
    ? 'flex flex-col flex-1 min-h-0 h-full rounded-[26px] border border-[rgba(var(--color-primary-light-rgb),0.35)] shadow-[0_-18px_45px_rgba(0,0,0,0.75)] bg-[rgba(4,8,18,0.95)] backdrop-blur-lg artifact-panel artifact-panel-mobile'
    : isMobile
      ? 'relative w-full h-full flex flex-col artifact-panel'
      : 'flex flex-col flex-1 min-h-0 h-full rounded-2xl border border-[rgba(var(--color-primary-light-rgb),0.3)] md:overflow-hidden overflow-visible shadow-2xl bg-gradient-to-br from-white/5 to-[rgba(var(--color-primary-rgb),0.05)] backdrop-blur-sm cosmic-ui-module artifact-panel p-0';

  const headerPaddingClass = isMobileEmbedded
    ? 'px-3 py-2.5'
    : 'px-3 py-3 sm:px-4 sm:py-4 md:px-6 md:py-5';

  const scrollPaddingClass = isMobile
    ? 'px-2 py-2 sm:px-3 sm:py-3'
    : 'px-2 py-2 sm:px-3 sm:py-3 md:p-6';

  return (
    <div className={containerClasses}>
      {/* Mobile backdrop */}
      {isMobile && !isEmbedded && (
        <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={onClose} />
      )}

      {/* Panel Content */}
      <div className={contentClasses} style={{ overflow: 'clip' }}>
        {/* Artifact Header - suppressed when embedded in mobile drawer since drawer already shows title */}
        {!isMobileEmbedded && (
        <div className="flex-shrink-0 bg-[rgba(0,0,0,0.6)] border-b border-[rgba(var(--color-primary-light-rgb),0.2)] backdrop-blur-xl">
            <div className={`flex items-center justify-between ${headerPaddingClass}`}>
              <div className="flex items-center gap-2 sm:gap-3 flex-1 min-w-0">
                <div className={`rounded-lg sm:rounded-xl bg-gradient-to-br from-[var(--color-primary)] to-[var(--color-secondary)] flex items-center justify-center shadow-lg flex-shrink-0 ${isMobile ? 'w-9 h-9' : 'w-9 h-9 sm:w-10 sm:h-10 md:w-12 md:h-12'}`}>
                  <span className={`${isMobile ? 'text-lg' : 'text-xl sm:text-2xl'}`}>🎨</span>
                </div>
                <div className="min-w-0 flex-1">
                  <h2 className={`${isMobile ? 'text-base font-semibold' : 'text-sm sm:text-lg md:text-xl font-bold'} text-white tracking-tight truncate`}>
                    Artifact
                  </h2>
                  <p className={`${isMobile ? 'text-[10px]' : 'text-[10px] sm:text-xs'} text-gray-400 truncate`}>
                    Canvas Output
                  </p>
                </div>
              </div>
              {showMobileClose ? (
                <button
                  onClick={onClose}
                  className="md:hidden w-9 h-9 sm:w-10 sm:h-10 rounded-lg sm:rounded-xl border border-white/20 bg-black/30 backdrop-blur-sm flex items-center justify-center text-white text-xl sm:text-2xl font-light transition-opacity duration-200 flex-shrink-0"
                  title="Close Artifact Canvas"
                  aria-label="Close Artifact Canvas"
                >
                  &times;
                </button>
              ) : (
                <div className="hidden md:block w-16 h-16" aria-hidden="true"></div>
              )}
            </div>
          </div>
        )}

          {/* Artifact Content Area - match chat scroll treatment */}
          <div className="flex-1 min-h-0 relative overflow-hidden" role="region" aria-label="Artifact output stream">
            <div className="absolute inset-0 pointer-events-none bg-[rgba(0,0,0,0.45)] z-0" />

            <div className={`absolute inset-0 overflow-y-auto ${scrollPaddingClass} my-scroll1 z-10 h-full flex flex-col`}>
              {/* If no content, show just the Mozaiks logo */}
              {(!messages || messages.length === 0) ? (
                <div className="flex flex-1 items-center justify-center min-h-full md:min-h-[500px]">
                  <div className="w-24 h-24 sm:w-32 sm:h-32 md:w-40 md:h-40 bg-gradient-to-br from-[var(--color-primary)]/20 to-[var(--color-secondary)]/20 rounded-2xl sm:rounded-3xl border-2 border-[var(--color-primary-light)]/50 flex items-center justify-center backdrop-blur-sm shadow-2xl">
                    <img
                      src="/mozaik_logo.svg"
                      alt="Mozaiks Logo"
                      className="w-16 h-16 sm:w-20 sm:h-20 md:w-28 md:h-28"
                      onError={(e) => {
                        e.target.onerror = null;
                        e.target.src = "/mozaik.png";
                      }}
                    />
                  </div>
                </div>
              ) : (
                <div className="space-y-3 sm:space-y-4 md:space-y-6 flex-1">
                  {messages.map((m, idx) => {
                    // If message has uiToolEvent, render the actual UI component
                    if (m.uiToolEvent && m.uiToolEvent.ui_tool_id) {
                      return (
                        <div key={m.id || idx} className="app-component-wrapper">
                          <UIToolRenderer
                            event={m.uiToolEvent}
                            onResponse={m.uiToolEvent.onResponse}
                            className="app-ui-component"
                          />
                        </div>
                      );
                    }

                    // Fallback: render as development view
                    let parsed = null;
                    try {
                      const jsonMatch = (typeof m.content === 'string') ? m.content.match(/\{[\s\S]*\}/) : null;
                      if (jsonMatch) parsed = JSON.parse(jsonMatch[0]);
                      else if (typeof m.content === 'object') parsed = m.content;
                    } catch (e) { parsed = null; }

                    return (
                      <div key={m.id || idx} className="bg-gray-800/40 border border-gray-700/60 rounded-lg sm:rounded-xl p-3 sm:p-4 md:p-5 backdrop-blur-sm">
                        <div className="flex items-start justify-between mb-2 sm:mb-3 gap-2">
                          <div className="min-w-0 flex-1">
                            <div className="text-[10px] sm:text-xs font-medium text-[var(--color-primary-light)] mb-1 truncate">{m.agentName || 'System'}</div>
                            <div className="text-xs sm:text-sm text-gray-200 font-semibold">Component Data</div>
                          </div>
                          <button
                            onClick={() => {
                              if (navigator.clipboard && parsed) {
                                navigator.clipboard.writeText(JSON.stringify(parsed, null, 2));
                              }
                            }}
                            className="text-[10px] sm:text-xs px-2 py-1 sm:px-3 sm:py-1.5 bg-gray-700/50 hover:bg-gray-600/50 rounded-md sm:rounded-lg text-gray-200 hover:text-white transition-all border border-gray-600/50 flex-shrink-0"
                          >
                            Copy
                          </button>
                        </div>
                        {parsed ? (
                          <pre className="text-[10px] sm:text-xs text-gray-300 bg-black/40 p-2 sm:p-3 md:p-4 rounded-lg overflow-auto max-h-60 sm:max-h-80 border border-gray-700/30 font-mono">
                            {JSON.stringify(parsed, null, 2)}
                          </pre>
                        ) : (
                          <div className="text-xs sm:text-sm text-gray-400">Component data not available.</div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

      </div>
    </div>
  );
};

export default ArtifactPanel;
