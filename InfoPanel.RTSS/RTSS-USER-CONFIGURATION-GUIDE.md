# RTSS User Configuration Guide

## Issue: Plugin Shows Stream Deck/Teams Instead of Games

If InfoPanel.RTSS is displaying Stream Deck (0.0 FPS) or Microsoft Teams instead of your games, this is likely a **user-specific RTSS configuration issue**.

### Quick Diagnostics

1. **Check what RTSS is hooking**: Open RTSS and look at the running applications list
2. **Verify game detection**: Ensure your games appear in RTSS when running
3. **Check MSI Afterburner settings**: Some settings affect what applications RTSS monitors

### Solution Steps

#### Step 1: RTSS Application Detection Settings
1. Open **RivaTuner Statistics Server** (RTSS)
2. Look for **Detection Level** or **Application Detection** settings
3. Try changing detection mode to be more selective about background apps

#### Step 2: MSI Afterburner Configuration  
1. Open **MSI Afterburner**
2. Go to **Settings** → **Monitoring**
3. Check if there are filters for which applications to monitor
4. Disable monitoring for obvious non-gaming applications

#### Step 3: Manual Application Profiles
1. In RTSS, create **application-specific profiles**
2. **Disable monitoring** for:
   - `StreamDeck.exe`
   - `Teams.exe` 
   - `Teams.msix` (new Teams)
   - Other utility applications
3. **Enable monitoring** explicitly for your games

#### Step 4: Process Priority in RTSS
- Some RTSS versions have settings that affect which process gets priority
- Look for "Stealth mode" or "Detection level" options
- Try different compatibility modes

### Expected Behavior
- **Games**: Should show actual FPS (30, 60, 120+ FPS)
- **Utility Apps**: Should either not appear or show 0.0 FPS and be ignored

### If Problem Persists
Please provide:
1. RTSS version number
2. MSI Afterburner version (if used)
3. Screenshot of RTSS running applications list
4. List of what applications you see in RTSS when the issue occurs

This allows for targeted troubleshooting rather than complex algorithmic solutions.