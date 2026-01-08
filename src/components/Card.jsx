// /src/components/Card.jsx

import React from 'react';

const Card = ({ 
  children, 
  title, 
  className = '',
  titleClassName = '',
  contentClassName = '',
  ...props 
}) => {
  return (
    <div 
      className={`bg-white border border-gray-200 rounded-lg shadow p-4 ${className}`}
      {...props}
    >
      {title && (
        <h3 className={`text-lg font-medium mb-3 text-text-primary ${titleClassName}`}>
          {title}
        </h3>
      )}
      <div className={contentClassName}>
        {children}
      </div>
    </div>
  );
};

export default Card;