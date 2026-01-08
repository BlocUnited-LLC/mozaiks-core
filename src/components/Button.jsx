// /src/components/Button.jsx

import React from 'react';

const Button = ({ 
  children, 
  variant = 'primary',
  size = 'md',
  className = '',
  ...props 
}) => {
  const baseClasses = 'font-medium rounded transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2';
  
  const variantClasses = {
    primary: 'bg-accent text-white hover:opacity-90 focus:ring-accent',
    secondary: 'bg-secondary text-text-primary border border-gray-300 hover:bg-gray-100 focus:ring-gray-400',
    outline: 'bg-transparent border border-accent text-accent hover:bg-accent hover:text-white focus:ring-accent',
    ghost: 'bg-transparent text-accent hover:bg-gray-100 focus:ring-accent',
  };
  
  const sizeClasses = {
    sm: 'py-1 px-3 text-sm',
    md: 'py-2 px-4',
    lg: 'py-3 px-6 text-lg',
  };
  
  const classes = [
    baseClasses,
    variantClasses[variant],
    sizeClasses[size],
    className
  ].join(' ');
  
  return (
    <button className={classes} {...props}>
      {children}
    </button>
  );
};

export default Button;