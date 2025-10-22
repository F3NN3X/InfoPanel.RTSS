# InfoPanel.RTSS - Simple Configuration-Based Solution

## Problem Summary
- **Issue**: One user reported Stream Deck (0.0 FPS) and MS Teams being detected instead of games
- **Investigation**: Only affects one user while working correctly for developer and other users
- **Conclusion**: Likely a user-specific RTSS configuration issue rather than fundamental algorithmic problem

## Solution Approach: User Configuration Instead of Complex Algorithm

Rather than implementing a complex 5-tier prioritization system for what appears to be an isolated configuration issue, we've implemented a **simple configuration-based filtering system** that users can adjust.

### Configuration Options (InfoPanel.RTSS.ini)

```ini
[Application_Filtering]
# Comma-separated list of process names to ignore (case-insensitive, without .exe)
ignored_processes=StreamDeck,Teams,Teams.msix,Discord,Chrome

# Only monitor applications with FPS above this threshold
minimum_fps_threshold=1.0

# Prefer fullscreen applications over windowed ones
prefer_fullscreen=true
```

### How It Works

1. **Early Filtering**: Applications are filtered during RTSS scanning, not after collection
2. **Configurable Thresholds**: Users can set minimum FPS thresholds and ignored process lists
3. **Simple Selection**: Prefer fullscreen if enabled, otherwise highest FPS
4. **User Control**: Users can customize behavior without code changes

### Benefits of This Approach

- ✅ **Simple**: Easy to understand and debug
- ✅ **User-Controllable**: No developer intervention needed for edge cases
- ✅ **Targeted**: Addresses the specific reported issue (Stream Deck, Teams)
- ✅ **Maintainable**: Minimal code complexity
- ✅ **Flexible**: Works for different user setups and preferences

### User Troubleshooting Steps

1. **Check ignored_processes**: Add problematic apps like `StreamDeck,Teams`
2. **Adjust minimum_fps_threshold**: Increase to filter out 0.0 FPS utility apps
3. **Toggle prefer_fullscreen**: Disable if alt-tabbing between games causes issues
4. **Enable debug logging**: Set `debug=true` to see what RTSS is detecting

## Why This is Better Than Complex Algorithm

- **Addresses Root Cause**: User-specific RTSS configuration variations
- **No Edge Cases**: No complex logic that might fail in unexpected scenarios
- **User Empowerment**: Users can fix their own issues with clear guidance
- **Future-Proof**: Works with any new problematic applications users encounter
- **Minimal Maintenance**: Less code to maintain and debug

## Implementation Changes

- Added configuration-based filtering in `RTSSOnlyMonitoringService`
- Simplified `SelectBestGamingCandidate` method (removed 5-tier priority system)
- Added early filtering during RTSS scanning for better performance
- Created user configuration guide and troubleshooting documentation

This approach turns a development problem into a user configuration problem, which is more appropriate for what appears to be an isolated setup-specific issue.