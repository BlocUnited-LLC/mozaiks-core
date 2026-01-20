// /src/components/Input.jsx

import React from 'react';

const Input = ({
  label,
  error,
  className = '',
  labelClassName = '',
  ...props
}) => {
  return (
    <div className="mb-4">
      {label && (
        <label className={`block text-text-primary font-medium mb-1 ${labelClassName}`}>
          {label}
        </label>
      )}
      <input
        className={`w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-accent focus:border-accent ${
          error ? 'border-red-500 focus:ring-red-500 focus:border-red-500' : ''
        } ${className}`}
        {...props}
      />
      {error && (
        <p className="mt-1 text-sm text-red-600">{error}</p>
      )}
    </div>
  );
};

export default Input;