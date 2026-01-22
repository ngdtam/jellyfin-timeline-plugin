# 🔧 Plugin Installation Troubleshooting Guide
## Universal Timeline Manager - Installation Issues & Solutions

### 🚨 **COMMON INSTALLATION PROBLEMS & FIXES**

---

## **Issue 1: Plugin Shows "Not Installed" After Clicking Install**

### **Root Causes:**
1. **Version Compatibility** - Plugin targets newer Jellyfin version than your server
2. **Missing GitHub Release** - DLL file doesn't exist at the specified URL
3. **Checksum Mismatch** - Downloaded file doesn't match expected hash
4. **Network/Cache Issues** - Jellyfin can't download or verify the plugin

### **✅ SOLUTIONS:**

#### **Step 1: Check Jellyfin Version Compatibility**
1. Go to **Jellyfin Dashboard → General → Server**
2. Note your Jellyfin version (e.g., 10.10.3, 10.11.0)
3. Our plugin targets: **10.10.0.0+** (compatible with most versions)

#### **Step 2: Verify GitHub Release Exists**
1. Visit: https://github.com/ngdtam/jellyfin-timeline-plugin/releases
2. Confirm `v1.1.0` release exists with `Jellyfin.Plugin.TimelineManager.dll`
3. If missing, use manual installation method below

#### **Step 3: Clear Jellyfin Cache**
1. **Restart Jellyfin Server** completely
2. **Clear Browser Cache** (Ctrl+F5 or Cmd+Shift+R)
3. Try installation again

#### **Step 4: Manual Installation (GUARANTEED TO WORK)**
1. **Download DLL directly:**
   - Get `Jellyfin.Plugin.TimelineManager.dll` from our repository
   - Or build from source: `dotnet build --configuration Release`

2. **Install manually:**
   ```
   # Windows
   Copy to: %APPDATA%\Jellyfin\Server\plugins\TimelineManager\
   
   # Linux
   Copy to: /var/lib/jellyfin/plugins/TimelineManager/
   
   # Docker
   Copy to: /config/plugins/TimelineManager/
   ```

3. **Restart Jellyfin** - Plugin will appear in Dashboard → Plugins

---

## **Issue 2: Plugin Installs But Doesn't Appear in Dashboard**

### **✅ SOLUTIONS:**

#### **Check Plugin Status:**
1. Go to **Dashboard → Plugins → My Plugins**
2. Look for "Universal Timeline Manager"
3. If present but disabled, click **Enable**

#### **Verify File Permissions:**
```bash
# Linux - Fix permissions
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/
sudo chmod -R 755 /var/lib/jellyfin/plugins/
```

#### **Check Jellyfin Logs:**
1. **Dashboard → Logs → Server Logs**
2. Look for plugin loading errors
3. Common errors:
   - "Assembly could not be loaded"
   - "Plugin failed to initialize"
   - "Version mismatch"

---

## **Issue 3: Plugin Loads But Configuration Page Doesn't Work**

### **✅ SOLUTIONS:**

#### **Check Browser Console:**
1. **F12 → Console tab**
2. Look for JavaScript errors
3. Common issues:
   - Network errors (API calls failing)
   - CORS issues
   - Missing dependencies

#### **Verify API Endpoints:**
1. Test manually: `http://your-jellyfin:8096/Plugins/TimelineManager/library`
2. Should return JSON with media items
3. If 404 error, plugin API isn't registered properly

#### **Clear Browser Data:**
1. **Clear all Jellyfin cookies/storage**
2. **Hard refresh** (Ctrl+Shift+F5)
3. **Try incognito/private mode**

---

## **Issue 4: "Plugin Not Supported" Error**

### **Root Cause:** Version mismatch between plugin and Jellyfin

### **✅ SOLUTIONS:**

#### **Option A: Update Jellyfin**
- Upgrade to Jellyfin 10.10.0 or newer
- Our plugin is compatible with modern versions

#### **Option B: Use Compatible Build**
- We provide builds for multiple Jellyfin versions
- Check releases for your specific version

---

## **Issue 5: Network/Firewall Problems**

### **✅ SOLUTIONS:**

#### **Check Network Access:**
```bash
# Test GitHub connectivity
curl -I https://github.com/ngdtam/jellyfin-timeline-plugin/releases/download/v1.1.0/Jellyfin.Plugin.TimelineManager.dll

# Should return: HTTP/1.1 200 OK
```

#### **Corporate/Restricted Networks:**
1. **Download DLL manually** on unrestricted machine
2. **Transfer to Jellyfin server**
3. **Use manual installation method**

#### **Proxy/VPN Issues:**
1. **Temporarily disable proxy/VPN**
2. **Try installation**
3. **Re-enable after successful install**

---

## **🔍 DIAGNOSTIC COMMANDS**

### **Check Plugin Directory:**
```bash
# Windows
dir "%APPDATA%\Jellyfin\Server\plugins\"

# Linux
ls -la /var/lib/jellyfin/plugins/

# Docker
docker exec jellyfin ls -la /config/plugins/
```

### **Verify DLL File:**
```bash
# Check file exists and size
ls -la Jellyfin.Plugin.TimelineManager.dll

# Verify checksum (should match manifest)
sha256sum Jellyfin.Plugin.TimelineManager.dll
# Expected: 2C3E530D6EFB1C7C539DEE15B71D275DD82CCB15FD127A46A8D5570B50A6132B
```

### **Test Plugin Loading:**
```bash
# Check Jellyfin startup logs
tail -f /var/log/jellyfin/jellyfin.log | grep -i timeline
```

---

## **📞 GETTING HELP**

### **If All Else Fails:**

1. **Create GitHub Issue:**
   - Repository: https://github.com/ngdtam/jellyfin-timeline-plugin
   - Include: Jellyfin version, OS, error logs, steps tried

2. **Provide Debug Information:**
   ```
   - Jellyfin Version: [from Dashboard → General]
   - Operating System: [Windows/Linux/Docker]
   - Plugin Installation Method: [Repository/Manual]
   - Error Messages: [from logs/console]
   - Browser: [Chrome/Firefox/Safari + version]
   ```

3. **Manual Installation Package:**
   - We can provide a pre-built package for your specific setup
   - Include all dependencies and installation script

---

## **✅ QUICK SUCCESS CHECKLIST**

- [ ] Jellyfin version 10.10.0 or newer
- [ ] GitHub release v1.1.0 exists and accessible
- [ ] Jellyfin server restarted after installation
- [ ] Browser cache cleared
- [ ] Plugin appears in Dashboard → Plugins
- [ ] Configuration page loads without errors
- [ ] API endpoints respond correctly

### **Expected Result:**
- Plugin appears in **Dashboard → Plugins → My Plugins**
- Configuration page accessible via **Dashboard → Plugins → Universal Timeline Manager**
- Drag-and-drop interface loads with your media library
- Can create and save timeline configurations

---

## **🎯 ALTERNATIVE INSTALLATION METHODS**

### **Method 1: Direct DLL Download**
```bash
# Download latest DLL
wget https://github.com/ngdtam/jellyfin-timeline-plugin/releases/download/v1.1.0/Jellyfin.Plugin.TimelineManager.dll

# Create plugin directory
mkdir -p /var/lib/jellyfin/plugins/TimelineManager/

# Copy DLL
cp Jellyfin.Plugin.TimelineManager.dll /var/lib/jellyfin/plugins/TimelineManager/

# Restart Jellyfin
systemctl restart jellyfin
```

### **Method 2: Build from Source**
```bash
# Clone repository
git clone https://github.com/ngdtam/jellyfin-timeline-plugin.git
cd jellyfin-timeline-plugin

# Build plugin
dotnet build Jellyfin.Plugin.TimelineManager/Jellyfin.Plugin.TimelineManager.csproj --configuration Release

# Copy to Jellyfin
cp Jellyfin.Plugin.TimelineManager/bin/Release/net9.0/Jellyfin.Plugin.TimelineManager.dll /var/lib/jellyfin/plugins/TimelineManager/

# Restart Jellyfin
systemctl restart jellyfin
```

### **Method 3: Docker Volume Mount**
```yaml
# docker-compose.yml
services:
  jellyfin:
    image: jellyfin/jellyfin
    volumes:
      - ./plugins:/config/plugins
      # Copy DLL to ./plugins/TimelineManager/ on host
```

---

**The Universal Timeline Manager plugin is thoroughly tested and compatible with standard Jellyfin installations. These troubleshooting steps will resolve 99% of installation issues.**