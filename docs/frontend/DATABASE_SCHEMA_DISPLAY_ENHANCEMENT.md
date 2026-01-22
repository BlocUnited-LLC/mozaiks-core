# Database Schema Display Enhancement

## Overview
Enhanced the Generator workflow's Data tab (ActionPlan UI) to display detailed database schema information when `CONTEXT_INCLUDE_SCHEMA=true`, providing users with rich context about available database collections and their structure.

## Problem Statement
The Data tab previously only showed:
- Connection status (Connected/Not Connected)
- List of collection names extracted from database variables
- Minimal context about what data is available

Users needed more detailed information during workflow generation to:
- Understand database structure (collections, fields, types)
- See which collections are app-specific
- Verify schema availability for agents to reference
- Make informed decisions about data integration

## Solution Implementation

### 1. Backend: Extract and Structure Schema Data
**File**: `workflows/Generator/tools/action_plan.py`

#### Added Schema Parsing Function
```python
def _parse_database_schema_info(
    schema_overview: Optional[str],
    collections_first_docs: Optional[Dict[str, Any]],
    context_schema_db: Optional[str],
    context_include_schema: bool
) -> Dict[str, Any]
```

**Purpose**: Parse `schema_overview` text format and `collections_first_docs` into structured JSON for UI consumption.

**Input Sources**:
- `schema_overview`: Text format schema from `core/workflow/context/variables.py` (line 293)
- `collections_first_docs_full`: Sample documents from each collection (line 338)
- `context_schema_db`: Database name from environment variable
- `context_include_schema`: Flag indicating schema access is enabled

**Output Structure**:
```json
{
  "enabled": true,
  "database_name": "MozaiksCore",
  "total_collections": 5,
  "collections": [
    {
      "name": "Users",
      "is_app": true,
      "fields": [
        {"name": "user_id", "type": "str"},
        {"name": "email", "type": "str"},
        {"name": "profile_data", "type": "dict"}
      ],
      "has_sample_data": true,
      "sample_doc_keys": ["_id", "user_id", "email", "app_id", ...]
    }
  ]
}
```

#### Schema Overview Text Format Parsing
Parses text format from `_get_database_schema_async()`:
```
DATABASE: MozaiksCore
TOTAL COLLECTIONS: 5

USERS [app-specific]:
  Fields:
    - user_id: str
    - email: str
    - created_at: datetime

CONCEPTS:
  Fields:
    - concept_id: ObjectId
    - title: str
    - overview: str
```

**Parsing Logic**:
1. Extract database name from "DATABASE:" line
2. Extract total collections count from "TOTAL COLLECTIONS:" line
3. Identify collection headers (UPPERCASE lines with ":")
4. Detect app-specific flag ("[app-specific]")
5. Parse field definitions ("- field_name: field_type")
6. Enhance with sample data availability from `collections_first_docs`

#### Integration Points

**Extract Context Variables** (line 745):
```python
schema_overview = context_variables.get("schema_overview")
collections_first_docs = context_variables.get("collections_first_docs_full")
context_include_schema = context_variables.get("context_include_schema", False)
context_schema_db = context_variables.get("context_schema_db")
```

**Parse and Include in Workflow** (line 1088):
```python
database_schema_info = _parse_database_schema_info(
    schema_overview=schema_overview,
    collections_first_docs=collections_first_docs,
    context_schema_db=context_schema_db,
    context_include_schema=context_include_schema
)

if database_schema_info.get("enabled"):
    plan_workflow["database_schema"] = database_schema_info
```

**Persist to Context Variables** (line 1179):
```python
if database_schema_info.get("enabled"):
    context_variables.set("database_schema_info", copy.deepcopy(database_schema_info))
```

### 2. Frontend: Enhanced Data Tab UI
**File**: `ChatUI/src/workflows/Generator/components/ActionPlan.js`

#### Database Schema Section
Replaced simple collection name list with rich schema display:

**Features**:
1. **Database Header**
   - Database name display
   - Total collections count badge
   - Clean, prominent styling

2. **Collections Grid**
   - Responsive grid layout (2-3 columns based on screen size)
   - Each collection card shows:
     * Collection name (bold, highlighted)
     * app-specific badge (if applicable)
     * Field list with types (up to 8 fields visible)
     * Overflow indicator (+N more fields)
     * Sample data availability indicator

3. **Field Type Display**
   - Monospace font for technical accuracy
   - Side-by-side field name and type
   - Scrollable list for collections with many fields
   - Subtle styling to distinguish from other sections

**Visual Example**:
```
┌─────────────────────────────────────────┐
│ Database: MozaiksCore       [5 Collections] │
├─────────────────────────────────────────┤
│ ┌───────────────┐  ┌───────────────┐  │
│ │ Users [App]│  │ Concepts      │  │
│ ├───────────────┤  ├───────────────┤  │
│ │ Fields (12)   │  │ Fields (8)    │  │
│ │ user_id   str │  │ concept_id  ObjectId│ 
│ │ email     str │  │ title       str│  │
│ │ created_at datetime│ overview   str│  │
│ │ ...           │  │ ...           │  │
│ │ +4 more fields│  │               │  │
│ │ ✓ Sample data │  │ ✓ Sample data │  │
│ └───────────────┘  └───────────────┘  │
└─────────────────────────────────────────┘
```

#### Fallback Behavior
Maintains backward compatibility with existing workflows:
```javascript
{/* Legacy: Database Collections from variables (fallback) */}
{contextAwareEnabled && !workflow?.database_schema && databaseCollections.size > 0 && (
  // ... simple collection name list
)}
```

## Data Flow

### Complete Pipeline

1. **Environment Configuration** (`.env`):
   ```
   CONTEXT_INCLUDE_SCHEMA=true
   CONTEXT_SCHEMA_DB=MozaiksCore
   ```

2. **Context Loading** (`core/workflow/context/variables.py`):
   - Line 323: Check `CONTEXT_INCLUDE_SCHEMA` flag
   - Line 330: Get `CONTEXT_SCHEMA_DB` name
   - Line 331: Call `_get_database_schema_async(db_name)`
   - Line 332: Extract `schema_overview` text
   - Line 336: Set `context.set("schema_overview", overview_text)`
   - Line 338: Fetch `collections_first_docs_full`

3. **Schema Generation** (`_get_database_schema_async()`, line 239):
   - Connect to MongoDB
   - List all collections
   - Sample one document per collection
   - Extract field names and types
   - Identify app-specific collections (have `app_id` field)
   - Format as text with "DATABASE:", "COLLECTIONS:", headers

4. **Action Plan Tool** (`workflows/Generator/tools/action_plan.py`):
   - Line 745: Extract schema context variables
   - Line 1088: Call `_parse_database_schema_info()`
   - Parse text format into structured JSON
   - Enhance with sample data indicators
   - Line 1094: Include in `plan_workflow["database_schema"]`
   - Line 1179: Persist to context as `database_schema_info`

5. **UI Transport** (`shared_app.py` WebSocket):
   - ActionPlanArchitect calls `save_action_plan()` tool
   - Tool returns `action_plan` with `workflow.database_schema`
   - WebSocket emits `ui_response` event with payload
   - React component receives artifact data

6. **UI Rendering** (`ActionPlan.js`):
   - Line 1008: Check `workflow?.database_schema` exists
   - Line 1012: Display database name and collection count
   - Line 1018: Map over `collections` array
   - Line 1020: Render collection cards with fields

## Benefits

### For Users
1. **Better Context**: See exactly what data is available during workflow planning
2. **Informed Decisions**: Understand field types when designing database integrations
3. **Transparency**: Know which collections are app-scoped
4. **Validation**: Verify schema matches expectations before workflow generation completes

### For Agents
1. **Rich Context**: Agents see same schema_overview text in their messages
2. **Accurate Design**: WorkflowArchitectAgent can reference actual field names
3. **Type Safety**: Tool manifests can specify correct data types based on schema
4. **Sample Data**: Access to `collections_first_docs_full` for realistic examples

### For Developers
1. **Debugging**: Visual confirmation of schema loading in UI
2. **Testing**: Easy verification that schema access is configured correctly
3. **Documentation**: Self-documenting workflow shows data dependencies
4. **Maintenance**: Quickly identify schema changes impacting workflows

## Integration with Database Manager

The displayed schema information aligns with `db_manager.py` operations:

**Database Manager Features** (from attachment):
- `save_to_database()` - Uses collection names shown in schema
- `load_from_database()` - Queries collections displayed in UI
- `update_in_database()` - Updates fields shown in schema display
- `delete_from_database()` - Removes from collections in schema

**Configuration Resolution** (`db_manager.py` line 385):
```python
def _get_database_config(workflow_name, database_name, collection_name):
    # Workflow config: database_manager.default_database/default_collection
    # OR explicit parameters
```

**Future Enhancement**: Could display `default_database` and `default_collection` from workflow config in UI to show db_manager defaults.

## Example Use Cases

### Use Case 1: User Profile Workflow
**Scenario**: Generate workflow for user onboarding

**Data Tab Display**:
```
Database: MozaiksCore [3 Collections]

┌─ Users [App] ─────────┐
│ Fields (8)                   │
│ user_id          str         │
│ email            str         │
│ app_id    ObjectId    │
│ profile_data     dict        │
│ created_at       datetime    │
│ updated_at       datetime    │
│ subscription_tier str        │
│ is_active        bool        │
│ ✓ Sample data available      │
└──────────────────────────────┘
```

**Agent Context**: WorkflowArchitectAgent sees `user_id`, `app_id`, etc. and creates appropriate database variables:
```json
{
  "name": "user_profile",
  "type": "database",
  "source": {
    "database_name": "MozaiksCore",
    "collection": "Users",
    "search_by": "user_id",
    "field": "profile_data"
  }
}
```

### Use Case 2: Analytics Dashboard Workflow
**Scenario**: Generate workflow for metrics reporting

**Data Tab Display**:
```
Database: MozaiksCore [4 Collections]

┌─ Events [App] ────────┐  ┌─ Metrics [App] ───────┐
│ Fields (12)                  │  │ Fields (9)                   │
│ event_id        ObjectId     │  │ metric_id      ObjectId      │
│ event_type      str          │  │ metric_name    str           │
│ user_id         str          │  │ metric_value   float         │
│ timestamp       datetime     │  │ aggregation    str           │
│ properties      dict         │  │ period_start   datetime      │
│ +7 more fields              │  │ period_end     datetime      │
│ ✓ Sample data available      │  │ +3 more fields              │
└──────────────────────────────┘  └──────────────────────────────┘
```

**Value**: User immediately sees what metrics and events are tracked, informing dashboard design decisions.

## Technical Implementation Details

### Schema Text Format Contract
Generated by `_get_database_schema_async()` in `core/workflow/context/variables.py`:

**Line 239-306**: Full implementation
- Line 250: Format "DATABASE: {name}"
- Line 251: Format "TOTAL COLLECTIONS: {count}"
- Line 263-276: Analyze each collection (sample doc, extract fields)
- Line 281-290: Format collection output with fields
- Line 293: Return `schema_info["schema_overview"]`

### JSON Structure Contract
Produced by `_parse_database_schema_info()`:

**Required Fields**:
- `enabled` (bool): Flag from CONTEXT_INCLUDE_SCHEMA
- `database_name` (str): Database name
- `total_collections` (int): Count of collections
- `collections` (array): Collection details

**Collection Object**:
- `name` (str): Collection name
- `is_app` (bool): Has app_id field
- `fields` (array): Field definitions
- `has_sample_data` (bool): Sample doc available (optional)
- `sample_doc_keys` (array): Sample document keys (optional)

**Field Object**:
- `name` (str): Field name
- `type` (str): Python type name (str, int, dict, datetime, ObjectId, etc.)

### UI Component Props
`DataView` component receives:
- `workflow` (object): Contains `database_schema` property
- `contextVariableDefinitions` (object): Variable definitions for other sections

## Error Handling

### Backend Resilience
1. **Missing Schema**: Returns `{"enabled": false}` if schema not available
2. **Parse Errors**: Catches exceptions, logs debug message, returns minimal info
3. **Malformed Text**: Skips unparseable lines, continues processing
4. **Missing Collections**: Empty array if no collections found

### Frontend Graceful Degradation
1. **No Schema Property**: Falls back to legacy collection name list
2. **Empty Collections**: Shows database header only, no grid
3. **Missing Fields**: Shows collection name without field details
4. **Conditional Rendering**: All sections wrapped in existence checks

## Testing Recommendations

### Backend Tests
```python
def test_parse_database_schema_with_valid_overview():
    schema_text = """DATABASE: MozaiksCore
TOTAL COLLECTIONS: 2

USERS [app-specific]:
  Fields:
    - user_id: str
    - email: str

CONCEPTS:
  Fields:
    - title: str
"""
    result = _parse_database_schema_info(
        schema_overview=schema_text,
        collections_first_docs=None,
        context_schema_db="MozaiksCore",
        context_include_schema=True
    )
    
    assert result["enabled"] == True
    assert result["database_name"] == "MozaiksCore"
    assert len(result["collections"]) == 2
    assert result["collections"][0]["is_app"] == True
```

### Frontend Tests
```javascript
describe('DataView - Database Schema Display', () => {
  it('shows database name and collection count', () => {
    const workflow = {
      database_schema: {
        enabled: true,
        database_name: 'MozaiksCore',
        total_collections: 3,
        collections: []
      }
    };
    
    render(<DataView workflow={workflow} />);
    expect(screen.getByText(/Database: MozaiksCore/)).toBeInTheDocument();
    expect(screen.getByText(/3 Collections/)).toBeInTheDocument();
  });
  
  it('displays collection fields with types', () => {
    // ... test field display
  });
});
```

## Performance Considerations

### Schema Loading
- **One-time Cost**: Schema loaded once per workflow generation session
- **Caching**: `schema_overview` and `collections_first_docs` cached in context variables
- **Truncation**: Text truncated to TRUNCATE_CHARS (8000) to prevent oversized messages
- **Async Loading**: `_get_database_schema_async()` is async, doesn't block workflow start

### UI Rendering
- **Lazy Rendering**: Only renders database section when `workflow.database_schema` exists
- **Field Limit**: Shows max 8 fields per collection, overflow hidden with indicator
- **Grid Layout**: Responsive grid prevents horizontal scroll on mobile

### Memory Footprint
- **Text Format**: ~1-2KB per collection in schema_overview
- **Structured JSON**: ~500 bytes per collection in database_schema
- **Sample Docs**: Limited to 10 keys per collection to prevent bloat

## Future Enhancements

### Phase 1: Database Manager Config Display
Show `default_database` and `default_collection` from workflow config:
```javascript
{workflow?.database_manager && (
  <div className="mb-4">
    <div className="text-xs text-slate-400">Database Manager Defaults</div>
    <div>Database: {workflow.database_manager.default_database}</div>
    <div>Collection: {workflow.database_manager.default_collection}</div>
  </div>
)}
```

### Phase 2: Interactive Schema Explorer
- Click collection card to expand full field list
- Show sample document values (anonymized)
- Display indexes and constraints
- Show collection size and document count

### Phase 3: Schema-Aware Tool Suggestions
- Suggest database variables based on available collections
- Validate tool manifests against actual schema
- Warn if referencing non-existent fields
- Auto-complete field names in tool parameters

### Phase 4: Real-Time Schema Updates
- WebSocket updates when schema changes
- Notification badge for schema drift
- Diff view showing schema changes since workflow creation
- Migration suggestions for breaking changes

## Files Modified

1. **workflows/Generator/tools/action_plan.py**
   - Added `_parse_database_schema_info()` function (line ~95)
   - Extract schema context variables (line 745)
   - Parse and include database_schema in workflow (line 1088)
   - Persist to context variables (line 1179)

2. **ChatUI/src/workflows/Generator/components/ActionPlan.js**
   - Enhanced DataView component (line 943)
   - Added database schema section (line 1008)
   - Collections grid with field types (line 1018)
   - Legacy fallback for backward compatibility (line 1057)

## Success Criteria

✅ Schema information extracted from context variables  
✅ Text format parsed into structured JSON  
✅ Database name and collection count displayed in UI  
✅ Collection cards show field names and types  
✅ app-specific collections marked with badge  
✅ Sample data availability indicated  
✅ Graceful fallback for legacy workflows  
✅ Responsive grid layout for mobile/desktop  
✅ Data persisted to context_variables for downstream agents  

---

**Date**: 2025-01-XX  
**Engineer**: EngineeringAgent  
**Session**: Database Schema Display Enhancement
