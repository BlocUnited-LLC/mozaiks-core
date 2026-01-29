/**
 * Plugin Page Container
 *
 * Standard container component for plugin pages.
 * Provides consistent layout, styling, and shell integration for plugin-provided pages.
 *
 * @module @mozaiks/shell/components/PluginPageContainer
 */

import React from 'react';
import { useBranding } from '../providers/BrandingProvider';

/**
 * PluginPageContainer Component
 *
 * Wraps plugin pages with consistent Shell styling and layout.
 * Provides access to shell context and ensures visual consistency.
 *
 * @param {Object} props
 * @param {React.ReactNode} props.children - Plugin page content
 * @param {string} props.title - Page title (displayed in header if showHeader=true)
 * @param {string} props.description - Optional page description
 * @param {boolean} props.showHeader - Whether to show the page header (default: true)
 * @param {boolean} props.fullWidth - Use full width layout (default: false)
 * @param {string} props.className - Additional CSS classes
 * @param {Object} props.route - Route configuration from navigation.json
 */
const PluginPageContainer = ({
  children,
  title = '',
  description = '',
  showHeader = true,
  fullWidth = false,
  className = '',
  route = {}
}) => {
  const { appName } = useBranding();

  // Get title from route meta if not provided directly
  const pageTitle = title || route?.meta?.title || '';
  const pageDescription = description || route?.meta?.description || '';

  // Update document title
  React.useEffect(() => {
    if (pageTitle) {
      document.title = `${pageTitle} - ${appName}`;
    }
  }, [pageTitle, appName]);

  return (
    <div
      className={`plugin-page-container min-h-screen bg-gray-50 dark:bg-gray-900 ${className}`}
    >
      {showHeader && pageTitle && (
        <header className="plugin-page-header bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
          <div className={`${fullWidth ? 'px-6' : 'max-w-7xl mx-auto px-4 sm:px-6 lg:px-8'} py-6`}>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
              {pageTitle}
            </h1>
            {pageDescription && (
              <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
                {pageDescription}
              </p>
            )}
          </div>
        </header>
      )}

      <main
        className={`plugin-page-content ${
          fullWidth ? 'px-6 py-6' : 'max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6'
        }`}
      >
        {children}
      </main>
    </div>
  );
};

/**
 * PluginCard - Standard card component for plugin content
 * Provides consistent styling for card-based layouts within plugin pages.
 */
export const PluginCard = ({
  children,
  title = '',
  description = '',
  className = '',
  padding = true
}) => (
  <div
    className={`plugin-card bg-white dark:bg-gray-800 rounded-lg shadow border border-gray-200 dark:border-gray-700 ${className}`}
  >
    {(title || description) && (
      <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
        {title && (
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">
            {title}
          </h2>
        )}
        {description && (
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            {description}
          </p>
        )}
      </div>
    )}
    <div className={padding ? 'p-6' : ''}>
      {children}
    </div>
  </div>
);

/**
 * PluginSection - Section component for organizing plugin page content
 */
export const PluginSection = ({
  children,
  title = '',
  description = '',
  className = ''
}) => (
  <section className={`plugin-section mb-8 ${className}`}>
    {(title || description) && (
      <div className="mb-4">
        {title && (
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            {title}
          </h2>
        )}
        {description && (
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            {description}
          </p>
        )}
      </div>
    )}
    {children}
  </section>
);

export default PluginPageContainer;
