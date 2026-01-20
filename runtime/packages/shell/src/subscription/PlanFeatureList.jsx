// src/subscription/PlanFeatureList.jsx
import React from 'react';

const PlanFeatureList = ({ plan, className = '' }) => {
  if (!plan || !plan.features || plan.features.length === 0) {
    return null;
  }
  
  return (
    <ul className={`text-text-secondary ${className}`}>
      {plan.features.map((feature, index) => (
        <li key={index} className="flex items-start mb-2">
          <svg className="h-5 w-5 text-accent mr-2 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
          <span>{feature}</span>
        </li>
      ))}
    </ul>
  );
};

export default PlanFeatureList;