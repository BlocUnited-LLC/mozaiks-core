# Practical Examples

Complete end-to-end examples showing Interactive Artifacts in action on the MozaiksAI platform.

---

## Example 1: Building an App with Revenue Tracking

**User Journey**: User creates a task management SaaS app, then checks its revenue.

### Backend: AppBuilder Workflow

```python
# workflows/appbuilder/appbuilder_workflow.py

from core.workflow import session_manager
from core.workflow.pack.gating import validate_pack_prereqs
import time

async def initialize_appbuilder(
    app_id: str,
    user_id: str,
    app_name: str,
    description: str
) -> dict:
    """
    Start AppBuilder workflow when user says "Build me a task management app".
    """
    # Step 1: Create workflow session
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="AppBuilder"
    )
    chat_id = session["_id"]
    
    # Step 2: Create artifact with initial state
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="AppBuilder",
        artifact_type="AppBuilderArtifact",
        initial_state={
            "app_name": app_name,
            "description": description,
            "architecture": None,
            "features": [],
            "build_progress": 0,
            "deployment_status": "not_started",
            "repository_url": None,
            "revenue_to_date": 0.0,
            "created_at": time.time(),
            "buttons": [
                {
                    "label": "View Revenue",
                    "action": "launch_workflow",
                    "workflow": "RevenueDashboard",
                    "enabled": False  # Disabled until app is deployed
                },
                {
                    "label": "Deploy to Staging",
                    "action": "deploy_app",
                    "environment": "staging"
                }
            ]
        }
    )
    
    # Step 3: Link them
    await session_manager.attach_artifact_to_session(
        chat_id=chat_id,
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return {
        "chat_id": chat_id,
        "artifact_id": artifact["_id"],
        "app_name": app_name
    }


async def update_app_architecture(
    artifact_id: str,
    app_id: str,
    architecture: str,
    features: list
):
    """
    Update artifact as AI generates the app architecture.
    """
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "architecture": architecture,
            "features": features,
            "build_progress": 25,
            "status_message": "Architecture designed"
        }
    )


async def generate_app_code(
    artifact_id: str,
    app_id: str,
    repository_url: str
):
    """
    Update artifact after code generation.
    """
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "repository_url": repository_url,
            "build_progress": 75,
            "status_message": "Code generated",
            "buttons": [
                {
                    "label": "View Revenue",
                    "action": "launch_workflow",
                    "workflow": "RevenueDashboard",
                    "enabled": False  # Still disabled until deployed
                },
                {
                    "label": "Deploy to Staging",
                    "action": "deploy_app",
                    "environment": "staging"
                },
                {
                    "label": "View Code",
                    "action": "open_repository",
                    "url": repository_url
                }
            ]
        }
    )


async def deploy_app(
    chat_id: str,
    artifact_id: str,
    app_id: str,
    environment: str
):
    """
    Deploy the app and enable revenue tracking.
    """
    # Simulate deployment
    deployment_url = f"https://{environment}.myapp.com"
    
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "deployment_status": environment,
            "deployment_url": deployment_url,
            "build_progress": 100,
            "status_message": f"Deployed to {environment}",
            "buttons": [
                {
                    "label": "View Revenue",
                    "action": "launch_workflow",
                    "workflow": "RevenueDashboard",
                    "enabled": True  # NOW enabled!
                },
                {
                    "label": "Deploy to Production",
                    "action": "deploy_app",
                    "environment": "production"
                },
                {
                    "label": "Launch Marketing",
                    "action": "launch_workflow",
                    "workflow": "MarketingAutomation",
                    "enabled": False  # Requires Generator to be COMPLETED
                }
            ]
        }
    )
    
    # Mark workflow as COMPLETED if deployed to production
    if environment == "production":
        await session_manager.complete_workflow_session(
            chat_id=chat_id,
            app_id=app_id
        )
```

### Frontend: AppBuilder Artifact Component

```tsx
// src/components/artifacts/AppBuilderArtifact.tsx

import React from 'react';
import { sendArtifactAction } from '@/utils/websocket';
import { Progress } from '@/components/ui/progress';
import { Button } from '@/components/ui/button';

interface AppBuilderState {
  app_name: string;
  description: string;
  architecture: string | null;
  features: string[];
  build_progress: number;
  deployment_status: string;
  deployment_url?: string;
  repository_url?: string;
  revenue_to_date: number;
  status_message?: string;
  buttons: Array<{
    label: string;
    action: string;
    workflow?: string;
    environment?: string;
    url?: string;
    enabled?: boolean;
  }>;
}

export function AppBuilderArtifact({ artifactId, chatId, state, ws }) {
  const handleButtonClick = (button: any) => {
    if (!ws || button.enabled === false) return;

    if (button.action === 'launch_workflow') {
      sendArtifactAction(
        ws,
        {
          action: 'launch_workflow',
          payload: {
            workflow_name: button.workflow,
            artifact_type: button.workflow === 'RevenueDashboard' 
              ? 'RevenueDashboard' 
              : 'MarketingDashboard'
          }
        },
        chatId
      );
    } else if (button.action === 'deploy_app') {
      sendArtifactAction(
        ws,
        {
          action: 'deploy_app',
          artifact_id: artifactId,
          payload: {
            environment: button.environment
          }
        },
        chatId
      );
    } else if (button.action === 'open_repository') {
      window.open(button.url, '_blank');
    }
  };

  return (
    <div className="p-6 bg-white rounded-lg shadow">
      {/* Header */}
      <div className="mb-4">
        <h2 className="text-2xl font-bold">{state.app_name}</h2>
        <p className="text-gray-600">{state.description}</p>
        <span className={`inline-block px-3 py-1 mt-2 rounded text-sm ${
          state.deployment_status === 'production' ? 'bg-green-100 text-green-800' :
          state.deployment_status === 'staging' ? 'bg-yellow-100 text-yellow-800' :
          'bg-gray-100 text-gray-800'
        }`}>
          {state.deployment_status.replace('_', ' ')}
        </span>
      </div>

      {/* Build Progress */}
      <div className="mb-6">
        <div className="flex justify-between mb-2">
          <label className="font-medium">Build Progress</label>
          <span>{state.build_progress}%</span>
        </div>
        <Progress value={state.build_progress} />
        {state.status_message && (
          <p className="text-sm text-gray-600 mt-1">{state.status_message}</p>
        )}
      </div>

      {/* Architecture */}
      {state.architecture && (
        <div className="mb-4">
          <label className="font-medium block mb-2">Architecture</label>
          <div className="bg-gray-50 p-3 rounded">
            <code className="text-sm">{state.architecture}</code>
          </div>
        </div>
      )}

      {/* Features */}
      {state.features.length > 0 && (
        <div className="mb-4">
          <label className="font-medium block mb-2">Features</label>
          <ul className="space-y-1">
            {state.features.map((feature, idx) => (
              <li key={idx} className="flex items-center">
                <span className="text-green-500 mr-2">✓</span>
                {feature}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Links */}
      <div className="mb-6 space-y-2">
        {state.repository_url && (
          <a 
            href={state.repository_url} 
            target="_blank" 
            rel="noopener noreferrer"
            className="text-blue-600 hover:underline block"
          >
            📦 View Repository
          </a>
        )}
        {state.deployment_url && (
          <a 
            href={state.deployment_url} 
            target="_blank" 
            rel="noopener noreferrer"
            className="text-blue-600 hover:underline block"
          >
            🚀 View Live App
          </a>
        )}
      </div>

      {/* Revenue */}
      {state.revenue_to_date > 0 && (
        <div className="mb-6 p-4 bg-green-50 rounded">
          <label className="font-medium block mb-1">Revenue to Date</label>
          <p className="text-3xl font-bold text-green-600">
            ${state.revenue_to_date.toFixed(2)}
          </p>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex flex-wrap gap-2">
        {state.buttons.map((button, idx) => (
          <Button
            key={idx}
            onClick={() => handleButtonClick(button)}
            disabled={button.enabled === false}
            variant={button.action === 'deploy_app' ? 'default' : 'outline'}
          >
            {button.label}
          </Button>
        ))}
      </div>
    </div>
  );
}
```

### User Flow

```
1. User: "Build me a task management SaaS app"
   → Backend: initialize_appbuilder()
   → Creates AppBuilder session + artifact
   → Frontend: Shows artifact with 0% progress

2. AI generates architecture: "Next.js + Supabase"
   → Backend: update_app_architecture()
   → Frontend: Artifact updates to 25% progress, shows architecture

3. AI generates code
   → Backend: generate_app_code()
   → Frontend: Artifact updates to 75%, shows GitHub link

4. User clicks "Deploy to Staging"
   → Frontend: sendArtifactAction({ action: 'deploy_app' })
   → Backend: deploy_app()
   → Frontend: Artifact updates to 100%, "View Revenue" button enabled

5. User clicks "View Revenue"
   → Frontend: sendArtifactAction({ action: 'launch_workflow', workflow: 'RevenueDashboard' })
   → Backend: Validates dependencies (app deployed ✓)
   → Backend: Creates RevenueDashboard session + artifact
   → Frontend: Receives chat.navigate event, switches to Revenue tab
```

---

## Example 2: Investment Flow (No Dependencies)

**User Journey**: User browses investment marketplace while building an app.

### Backend: Investment Marketplace

```python
# workflows/investment/investment_workflow.py

from core.workflow import session_manager

async def create_investment_marketplace(
    app_id: str,
    user_id: str
):
    """
    Launch Investment Marketplace (no dependencies required).
    """
    # Fetch available apps
    available_apps = await fetch_investable_apps(app_id, user_id)
    
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="InvestmentMarketplace"
    )
    
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="InvestmentMarketplace",
        artifact_type="InvestmentMarketplace",
        initial_state={
            "apps": available_apps,
            "filters": {
                "category": "all",
                "min_revenue": 0,
                "sort_by": "revenue_desc"
            },
            "selected_app": None,
            "user_investments": []
        }
    )
    
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return {
        "chat_id": session["_id"],
        "artifact_id": artifact["_id"]
    }


async def fetch_investable_apps(app_id: str, user_id: str) -> list:
    """
    Fetch apps available for investment (excluding user's own apps).
    """
    # Query MongoDB for deployed apps
    # Exclude apps where creator == user_id
    return [
        {
            "app_id": "app_001",
            "app_name": "E-Commerce Platform",
            "creator": "user_789",
            "creator_name": "Jane Developer",
            "category": "ecommerce",
            "revenue_30d": 1250.50,
            "asking_price": 5000.00,
            "roi_estimate": 25.0,
            "description": "Multi-vendor marketplace with payments",
            "tech_stack": ["Next.js", "Stripe", "MongoDB"]
        },
        {
            "app_id": "app_002",
            "app_name": "Fitness Tracker",
            "creator": "user_456",
            "creator_name": "Bob Coder",
            "category": "saas",
            "revenue_30d": 890.00,
            "asking_price": 3000.00,
            "roi_estimate": 29.6,
            "description": "Track workouts and nutrition",
            "tech_stack": ["React Native", "Firebase"]
        }
    ]


async def invest_in_app(
    artifact_id: str,
    app_id: str,
    user_id: str,
    app_id: str,
    amount: float
):
    """
    Process investment and update artifact.
    """
    # Process payment
    investment_id = await process_investment(user_id, app_id, amount)
    
    # Update artifact to reflect new investment
    artifact = await session_manager.get_artifact_instance(artifact_id, app_id)
    user_investments = artifact["state"].get("user_investments", [])
    user_investments.append({
        "investment_id": investment_id,
        "app_id": app_id,
        "amount": amount,
        "invested_at": time.time()
    })
    
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "user_investments": user_investments,
            "selected_app": app_id
        }
    )
```

### Frontend: Investment Marketplace Component

```tsx
// src/components/artifacts/InvestmentMarketplace.tsx

import React, { useState } from 'react';
import { sendArtifactAction } from '@/utils/websocket';
import { toast } from 'sonner';

export function InvestmentMarketplace({ artifactId, chatId, state, ws }) {
  const [investmentAmount, setInvestmentAmount] = useState(100);
  const [selectedApp, setSelectedApp] = useState<string | null>(null);

  const handleInvest = (app: any) => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'invest_in_app',
        artifact_id: artifactId,
        payload: {
          app_id: app.app_id,
          amount: investmentAmount
        }
      },
      chatId
    );

    toast.success(`Invested $${investmentAmount} in ${app.app_name}`);
  };

  const handleFilterChange = (category: string) => {
    sendArtifactAction(
      ws,
      {
        action: 'update_state',
        artifact_id: artifactId,
        payload: {
          state_updates: {
            filters: { ...state.filters, category }
          }
        }
      },
      chatId
    );
  };

  const filteredApps = state.apps.filter(app => {
    if (state.filters.category !== 'all' && app.category !== state.filters.category) {
      return false;
    }
    if (app.revenue_30d < state.filters.min_revenue) {
      return false;
    }
    return true;
  });

  return (
    <div className="p-6">
      <div className="mb-6">
        <h2 className="text-2xl font-bold mb-2">Investment Marketplace</h2>
        <p className="text-gray-600">
          Browse and invest in apps built by others on the platform
        </p>
      </div>

      {/* Filters */}
      <div className="mb-6 flex gap-4">
        <select
          value={state.filters.category}
          onChange={(e) => handleFilterChange(e.target.value)}
          className="border rounded px-3 py-2"
        >
          <option value="all">All Categories</option>
          <option value="saas">SaaS</option>
          <option value="ecommerce">E-Commerce</option>
          <option value="marketplace">Marketplace</option>
        </select>

        <div className="flex items-center gap-2">
          <label>Investment Amount:</label>
          <input
            type="number"
            value={investmentAmount}
            onChange={(e) => setInvestmentAmount(Number(e.target.value))}
            min={10}
            step={10}
            className="border rounded px-3 py-2 w-32"
          />
        </div>
      </div>

      {/* Apps Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {filteredApps.map(app => (
          <div key={app.app_id} className="border rounded-lg p-4 hover:shadow-lg transition">
            <h3 className="font-bold text-lg mb-2">{app.app_name}</h3>
            <p className="text-sm text-gray-600 mb-3">{app.description}</p>
            
            <div className="mb-3">
              <span className="text-xs bg-blue-100 text-blue-800 px-2 py-1 rounded">
                {app.category}
              </span>
              <p className="text-sm mt-2">by {app.creator_name}</p>
            </div>

            <div className="mb-3 space-y-1">
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">30-day revenue:</span>
                <span className="font-medium">${app.revenue_30d.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-600">ROI estimate:</span>
                <span className="font-medium text-green-600">{app.roi_estimate}%</span>
              </div>
            </div>

            <div className="mb-3">
              <label className="text-xs text-gray-600">Tech Stack:</label>
              <div className="flex flex-wrap gap-1 mt-1">
                {app.tech_stack.map((tech, idx) => (
                  <span key={idx} className="text-xs bg-gray-100 px-2 py-1 rounded">
                    {tech}
                  </span>
                ))}
              </div>
            </div>

            <button
              onClick={() => handleInvest(app)}
              className="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 transition"
            >
              Invest ${investmentAmount}
            </button>
          </div>
        ))}
      </div>

      {/* User's Investments */}
      {state.user_investments.length > 0 && (
        <div className="mt-8">
          <h3 className="text-xl font-bold mb-4">Your Investments</h3>
          <div className="space-y-2">
            {state.user_investments.map((inv, idx) => {
              const app = state.apps.find(a => a.app_id === inv.app_id);
              return (
                <div key={idx} className="flex justify-between items-center border p-3 rounded">
                  <div>
                    <span className="font-medium">{app?.app_name}</span>
                    <span className="text-sm text-gray-600 ml-2">
                      {new Date(inv.invested_at * 1000).toLocaleDateString()}
                    </span>
                  </div>
                  <span className="font-bold">${inv.amount.toFixed(2)}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
```

### User Flow

```
1. User (mid-AppBuilder): "Show me apps I can invest in"
   → AI: "Opening investment marketplace..."
   → Backend: create_investment_marketplace()
   → Frontend: Receives chat.navigate, opens new tab "InvestmentMarketplace"
   → AppBuilder session stays IN_PROGRESS

2. User browses apps, filters by "SaaS"
   → Frontend: sendArtifactAction({ action: 'update_state', filters: {...} })
   → Backend: update_artifact_state()
   → Frontend: Artifact re-renders with filtered apps

3. User: "Should I invest in the Fitness Tracker app?"
   → AI analyzes app data in artifact state
   → AI: "Strong ROI of 29.6%, consistent revenue growth, good tech stack..."

4. User clicks "Invest $100" on Fitness Tracker
   → Frontend: sendArtifactAction({ action: 'invest_in_app' })
   → Backend: invest_in_app(), processes payment
   → Frontend: Toast "Invested $100 in Fitness Tracker", artifact updates

5. User switches back to AppBuilder tab
   → Frontend: setActiveSessionIndex(0)
   → AppBuilder artifact still showing same progress (state preserved)
```

---

## Example 3: Dependency Blocking (Marketing Automation)

**User Journey**: User tries to launch Marketing but Generator not complete.

### Backend: Marketing Workflow with Dependencies

```python
# workflows/marketing/marketing_workflow.py

# WorkflowDependencies collection already has this:
# {
#   "app_id": "ent_001",
#   "workflows": {
#     "MarketingAutomation": {
#       "dependencies": {
#         "required_workflows": [
#           {
#             "workflow": "Generator",
#             "status": "COMPLETED",
#             "reason": "Marketing requires a fully generated app to promote"
#           }
#         ]
#       }
#     }
#   }
# }

async def create_marketing_automation(
    app_id: str,
    user_id: str,
    app_id: str
):
    """
    Create Marketing Automation workflow.
    
    NOTE: Dependency validation already happened in simple_transport.py
    This function only runs if Generator workflow is COMPLETED.
    """
    app_info = await get_app_info(app_id, app_id)
    
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="MarketingAutomation"
    )
    
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="MarketingAutomation",
        artifact_type="MarketingDashboard",
        initial_state={
            "app_id": app_id,
            "app_name": app_info["name"],
            "campaigns": [],
            "target_audience": None,
            "budget": 0,
            "channels": {
                "email": {"enabled": False, "cost": 0},
                "social": {"enabled": False, "cost": 0},
                "seo": {"enabled": False, "cost": 0}
            },
            "performance": {
                "impressions": 0,
                "clicks": 0,
                "conversions": 0
            }
        }
    )
    
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return {
        "chat_id": session["_id"],
        "artifact_id": artifact["_id"]
    }
```

### Frontend: Handling Dependency Blocked

```tsx
// src/components/chat/ChatWithArtifacts.tsx

import { toast } from 'sonner';

export function ChatWithArtifacts() {
  // (Include useState for sessions, useArtifactEvents hook, etc. from Example 1)

  const handleDependencyBlocked = (event: DependencyBlockedEvent) => {
    const { workflow_name, message } = event.data;

    // Show error toast with action
    toast.error(message, {
      description: `Cannot launch ${workflow_name}`,
      duration: 7000,
      action: {
        label: 'View Requirements',
        onClick: () => {
          // Maybe show a modal with dependency details
          showDependencyModal(workflow_name);
        }
      }
    });

    // Log for debugging
    console.warn('❌ Dependency blocked:', event.data);
  };

  const showDependencyModal = (workflowName: string) => {
    // Show modal explaining what needs to be completed
    // Example: "Marketing Automation requires Generator workflow to be completed"
  };

  // (Return JSX with chat interface, session tabs, and artifact rendering)
}
```

### User Flow

```
1. User in AppBuilder (build_progress: 50%, status: IN_PROGRESS)
   → User sees "Launch Marketing" button (enabled in UI)

2. User clicks "Launch Marketing"
   → Frontend: sendArtifactAction({ action: 'launch_workflow', workflow: 'MarketingAutomation' })
   → Backend: simple_transport.py receives action
   → Backend: validate_pack_prereqs(app_id=..., user_id=..., workflow_name='MarketingAutomation')
   → Backend: Queries WorkflowDependencies → requires Generator COMPLETED
   → Backend: Queries ChatSessions → finds Generator with status=IN_PROGRESS ❌
   → Backend: Returns (False, "The Generator workflow must be completed before starting MarketingAutomation")

3. Backend sends chat.dependency_blocked event
   → Frontend: handleDependencyBlocked() triggered
   → Toast appears: "Please complete the Generator workflow first. Marketing automation requires your app to be fully generated."
   → NO navigation happens, NO session created

4. User completes Generator workflow
   → Backend: complete_workflow_session(chat_id, app_id)
   → Generator status → COMPLETED

5. User clicks "Launch Marketing" again
   → Backend: validate_pack_prereqs(app_id=..., user_id=..., workflow_name='MarketingAutomation')
   → Backend: Queries ChatSessions → finds Generator with status=COMPLETED ✓
   → Backend: create_marketing_automation() runs
   → Frontend: Receives chat.navigate, opens Marketing tab
```

---

## Example 4: Real-Time State Sync (Challenge Tracker)

**User Journey**: User participates in coding challenge, progress syncs across devices.

### Backend: Challenge Tracker

```python
# workflows/challenge/challenge_workflow.py

async def join_challenge(
    app_id: str,
    user_id: str,
    challenge_id: str
):
    """
    User joins a coding challenge.
    """
    challenge_info = await get_challenge_info(challenge_id)
    
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="ChallengeTracker"
    )
    
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="ChallengeTracker",
        artifact_type="ChallengeTracker",
        initial_state={
            "challenge_id": challenge_id,
            "challenge_name": challenge_info["name"],
            "total_steps": challenge_info["total_steps"],
            "completed_steps": 0,
            "progress": 0,
            "steps": challenge_info["steps"],
            "submission_url": None,
            "status": "in_progress",
            "started_at": time.time(),
            "deadline": challenge_info["deadline"]
        }
    )
    
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return {
        "chat_id": session["_id"],
        "artifact_id": artifact["_id"]
    }


async def complete_challenge_step(
    artifact_id: str,
    app_id: str,
    step_number: int,
    submission_data: dict
):
    """
    Mark a challenge step as complete.
    
    This will broadcast to ALL connected clients viewing this artifact.
    """
    artifact = await session_manager.get_artifact_instance(artifact_id, app_id)
    completed = artifact["state"]["completed_steps"] + 1
    total = artifact["state"]["total_steps"]
    progress = (completed / total) * 100
    
    # Update step status
    steps = artifact["state"]["steps"]
    steps[step_number - 1]["status"] = "completed"
    steps[step_number - 1]["completed_at"] = time.time()
    steps[step_number - 1]["submission"] = submission_data
    
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "completed_steps": completed,
            "progress": progress,
            "steps": steps,
            "last_activity": time.time()
        }
    )
    
    # Check if challenge complete
    if completed >= total:
        await session_manager.update_artifact_state(
            artifact_id=artifact_id,
            app_id=app_id,
            state_updates={
                "status": "completed",
                "completed_at": time.time()
            }
        )
```

### Frontend: Challenge Tracker with Real-Time Updates

```tsx
// src/components/artifacts/ChallengeTracker.tsx

import React, { useEffect } from 'react';
import { sendArtifactAction } from '@/utils/websocket';

export function ChallengeTracker({ artifactId, chatId, state, ws }) {
  // Listen for state updates from backend
  useEffect(() => {
    console.log('🔄 Challenge progress updated:', state.progress);
  }, [state.progress, state.completed_steps]);

  const handleCompleteStep = (stepNumber: number, submissionData: any) => {
    sendArtifactAction(
      ws,
      {
        action: 'complete_challenge_step',
        artifact_id: artifactId,
        payload: {
          step_number: stepNumber,
          submission_data: submissionData
        }
      },
      chatId
    );
  };

  const handleSubmitChallenge = () => {
    sendArtifactAction(
      ws,
      {
        action: 'submit_challenge',
        artifact_id: artifactId,
        payload: {
          submission_url: state.submission_url
        }
      },
      chatId
    );
  };

  const timeRemaining = state.deadline - Date.now() / 1000;
  const hoursRemaining = Math.floor(timeRemaining / 3600);

  return (
    <div className="p-6">
      <div className="mb-6">
        <h2 className="text-2xl font-bold">{state.challenge_name}</h2>
        <div className="flex items-center gap-4 mt-2">
          <span className={`px-3 py-1 rounded text-sm ${
            state.status === 'completed' ? 'bg-green-100 text-green-800' :
            'bg-blue-100 text-blue-800'
          }`}>
            {state.status}
          </span>
          <span className="text-sm text-gray-600">
            ⏰ {hoursRemaining}h remaining
          </span>
        </div>
      </div>

      {/* Progress */}
      <div className="mb-6">
        <div className="flex justify-between mb-2">
          <span className="font-medium">Progress</span>
          <span>{state.completed_steps} / {state.total_steps} steps</span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-4">
          <div 
            className="bg-blue-600 h-4 rounded-full transition-all duration-500"
            style={{ width: `${state.progress}%` }}
          />
        </div>
      </div>

      {/* Steps */}
      <div className="space-y-4">
        {state.steps.map((step, idx) => (
          <div 
            key={idx}
            className={`border rounded-lg p-4 ${
              step.status === 'completed' ? 'bg-green-50 border-green-200' :
              'bg-white'
            }`}
          >
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <h3 className="font-medium">
                  {idx + 1}. {step.title}
                </h3>
                <p className="text-sm text-gray-600 mt-1">{step.description}</p>
              </div>
              {step.status === 'completed' ? (
                <span className="text-green-600 text-xl">✓</span>
              ) : (
                <button
                  onClick={() => handleCompleteStep(idx + 1, { code: 'example' })}
                  className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
                >
                  Mark Complete
                </button>
              )}
            </div>
            {step.submission && (
              <div className="mt-3 p-2 bg-gray-50 rounded text-sm">
                <span className="text-gray-600">Completed: </span>
                {new Date(step.completed_at * 1000).toLocaleString()}
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Submit Button */}
      {state.status !== 'completed' && state.completed_steps === state.total_steps && (
        <div className="mt-6">
          <button
            onClick={handleSubmitChallenge}
            className="w-full bg-green-600 text-white py-3 rounded-lg font-medium hover:bg-green-700"
          >
            Submit Challenge 🎉
          </button>
        </div>
      )}

      {state.status === 'completed' && (
        <div className="mt-6 p-4 bg-green-50 border border-green-200 rounded-lg text-center">
          <span className="text-2xl">🎉</span>
          <h3 className="font-bold text-lg mt-2">Challenge Completed!</h3>
          <p className="text-sm text-gray-600">
            Submitted {new Date(state.completed_at * 1000).toLocaleString()}
          </p>
        </div>
      )}
    </div>
  );
}
```

---

## 📚 Summary

These examples demonstrate:

1. **AppBuilder → Revenue**: Creating artifacts with dynamic buttons, state progression, enabling features conditionally
2. **Investment Marketplace**: No dependencies, filtering, real-time investment tracking
3. **Marketing Automation**: Dependency blocking, clear error handling, requirement validation
4. **Challenge Tracker**: Real-time state sync, progress tracking, multi-step workflows

All examples follow the same patterns:
- Backend creates session + artifact
- Backend updates artifact state as workflow progresses
- Frontend listens for state updates and re-renders
- Actions trigger backend operations via `sendArtifactAction`

---

## 🔗 Next Steps

- **Troubleshooting** → [`06-TROUBLESHOOTING.md`](./06-TROUBLESHOOTING.md) - Debug issues
- **Backend Integration** → [`03-BACKEND-INTEGRATION.md`](./03-BACKEND-INTEGRATION.md) - Review backend patterns
- **Frontend Integration** → [`04-FRONTEND-INTEGRATION.md`](./04-FRONTEND-INTEGRATION.md) - Review frontend patterns
