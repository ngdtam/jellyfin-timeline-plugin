// Universal Timeline Manager - Configuration Page JavaScript
console.log('=== TIMELINE MANAGER SCRIPT LOADED ===');

// Initialize on page load
(function() {
    console.log('Script executing...');
    
    // Load playlists after a short delay to ensure DOM is ready
    setTimeout(function() {
        var container = document.getElementById('playlistsContainer');
        if (container) {
            console.log('Container found');
            loadPlaylists();
        } else {
            console.log('Container NOT found');
        }
    }, 500);
})();

// Function to load and display playlists
function loadPlaylists() {
    console.log('loadPlaylists called');
    var playlistsContainer = document.getElementById('playlistsContainer');
    
    if (!playlistsContainer) {
        console.error('playlistsContainer not found!');
        return;
    }
    
    try {
        var apiKey = ApiClient.accessToken();
        var userId = ApiClient.getCurrentUserId();
        
        console.log('API Key:', apiKey ? 'Found' : 'NOT FOUND');
        console.log('User ID:', userId);
        
        if (!apiKey) {
            playlistsContainer.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Not authenticated. Please log in first.</div>';
            return;
        }
        
        playlistsContainer.innerHTML = '<div class="fieldDescription">Loading playlists...</div>';
        
        // Get all playlists for the user
        fetch('/Users/' + userId + '/Items?IncludeItemTypes=Playlist&Recursive=true&Fields=DateCreated,ChildCount', {
            headers: {
                'X-Emby-Token': apiKey
            }
        })
        .then(function(response) {
            console.log('Fetch response:', response.status);
            return response.json();
        })
        .then(function(data) {
            console.log('Got data:', data);
            var playlists = data.Items || [];
            console.log('Playlists count:', playlists.length);
            
            if (playlists.length === 0) {
                playlistsContainer.innerHTML = '<div class="fieldDescription">No playlists found. Create one using the button above.</div>';
                return;
            }
            
            // Display playlists
            playlistsContainer.innerHTML = '';
            var container = document.createElement('div');
            container.style.display = 'grid';
            container.style.gap = '1em';
            
            playlists.forEach(function(playlist) {
                var card = document.createElement('div');
                card.className = 'paperList';
                card.style.padding = '1em';
                card.style.display = 'flex';
                card.style.justifyContent = 'space-between';
                card.style.alignItems = 'center';
                card.style.border = '1px solid #333';
                
                var info = document.createElement('div');
                
                var name = document.createElement('div');
                name.style.fontWeight = '500';
                name.style.marginBottom = '0.5em';
                name.textContent = playlist.Name;
                
                var details = document.createElement('div');
                details.className = 'fieldDescription';
                details.textContent = (playlist.ChildCount || 0) + ' items • Created: ' + new Date(playlist.DateCreated).toLocaleDateString();
                
                info.appendChild(name);
                info.appendChild(details);
                
                var deleteBtn = document.createElement('button');
                deleteBtn.type = 'button';
                deleteBtn.className = 'emby-button raised';
                deleteBtn.textContent = 'Delete';
                deleteBtn.style.background = '#f44336';
                deleteBtn.style.color = 'white';
                deleteBtn.onclick = function() {
                    deletePlaylist(playlist.Id, playlist.Name);
                };
                
                card.appendChild(info);
                card.appendChild(deleteBtn);
                container.appendChild(card);
            });
            
            playlistsContainer.appendChild(container);
            console.log('Playlists displayed successfully');
        })
        .catch(function(error) {
            console.error('Error loading playlists:', error);
            playlistsContainer.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Error loading playlists: ' + error.message + '</div>';
        });
        
    } catch (error) {
        console.error('Exception in loadPlaylists:', error);
        playlistsContainer.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Error loading playlists: ' + error.message + '</div>';
    }
}

// Function to delete a playlist
function deletePlaylist(playlistId, playlistName) {
    if (!confirm('Are you sure you want to delete "' + playlistName + '"?')) {
        return;
    }
    
    try {
        var apiKey = ApiClient.accessToken();
        
        fetch('/Items/' + playlistId, {
            method: 'DELETE',
            headers: {
                'X-Emby-Token': apiKey
            }
        })
        .then(function(response) {
            if (response.ok) {
                alert('Playlist "' + playlistName + '" deleted successfully!');
                loadPlaylists(); // Reload the list
            } else {
                alert('Failed to delete playlist: ' + response.statusText);
            }
        })
        .catch(function(error) {
            alert('Error deleting playlist: ' + error.message);
        });
    } catch (error) {
        alert('Error deleting playlist: ' + error.message);
    }
}

// Create Playlists button handler
function createPlaylists() {
    console.log('Create playlists button clicked');
    
    var createBtn = document.getElementById('createPlaylistsBtn');
    var statusMessage = document.getElementById('statusMessage');
    var outputBox = document.getElementById('outputBox');
    var errorBox = document.getElementById('errorBox');
    
    if (!createBtn) {
        console.error('Create button not found!');
        return;
    }
    
    // Hide previous messages
    statusMessage.style.display = 'none';
    outputBox.style.display = 'none';
    errorBox.style.display = 'none';
    
    // Show loading state
    createBtn.disabled = true;
    createBtn.textContent = 'Creating...';
    
    statusMessage.style.display = 'block';
    statusMessage.style.background = '#1c4966';
    statusMessage.style.color = '#fff';
    statusMessage.textContent = 'Creating playlists from configuration...';
    
    try {
        var apiKey = ApiClient.accessToken();
        var userId = ApiClient.getCurrentUserId();
        
        if (!apiKey) {
            throw new Error('Not authenticated. Please log in first.');
        }
        
        if (!userId) {
            throw new Error('User ID not found. Please log in first.');
        }
        
        console.log('Creating playlists for user:', userId);
        
        // Call the CreatePlaylists API endpoint with userId query parameter
        fetch('/Timeline/CreatePlaylists?userId=' + userId, {
            method: 'POST',
            headers: {
                'X-Emby-Token': apiKey,
                'Content-Type': 'application/json'
            }
        })
        .then(function(response) {
            console.log('Create playlists response:', response.status);
            return response.json();
        })
        .then(function(data) {
            console.log('Create playlists result:', data);
            
            // Reset button
            createBtn.disabled = false;
            createBtn.textContent = 'Create Playlists';
            
            if (data.success) {
                // Show success message
                statusMessage.style.background = '#1e5631';
                statusMessage.style.color = '#fff';
                statusMessage.textContent = 'Playlists created successfully!';
                
                // Show output details
                if (data.output) {
                    outputBox.style.display = 'block';
                    outputBox.textContent = data.output;
                }
                
                // Refresh the playlist list after 1 second
                setTimeout(function() {
                    loadPlaylists();
                }, 1000);
            } else {
                // Show error
                statusMessage.style.background = '#5a1a1a';
                statusMessage.style.color = '#ff6b6b';
                statusMessage.textContent = 'Failed to create playlists';
                
                if (data.error) {
                    errorBox.style.display = 'block';
                    errorBox.textContent = data.error;
                }
            }
        })
        .catch(function(error) {
            console.error('Error creating playlists:', error);
            
            // Reset button
            createBtn.disabled = false;
            createBtn.textContent = 'Create Playlists';
            
            // Show error
            statusMessage.style.display = 'block';
            statusMessage.style.background = '#5a1a1a';
            statusMessage.style.color = '#ff6b6b';
            statusMessage.textContent = 'Error creating playlists';
            
            errorBox.style.display = 'block';
            errorBox.textContent = error.message;
        });
        
    } catch (error) {
        console.error('Exception in createPlaylists:', error);
        
        // Reset button
        createBtn.disabled = false;
        createBtn.textContent = 'Create Playlists';
        
        // Show error
        statusMessage.style.display = 'block';
        statusMessage.style.background = '#5a1a1a';
        statusMessage.style.color = '#ff6b6b';
        statusMessage.textContent = 'Error: ' + error.message;
    }
}

// Refresh button handler
setTimeout(function() {
    var refreshBtn = document.getElementById('refreshPlaylistsBtn');
    if (refreshBtn) {
        refreshBtn.addEventListener('click', function() {
            console.log('Refresh button clicked');
            loadPlaylists();
        });
    }
    
    // Create button handler
    var createBtn = document.getElementById('createPlaylistsBtn');
    if (createBtn) {
        createBtn.addEventListener('click', function() {
            console.log('Create button clicked');
            createPlaylists();
        });
    }
}, 500);

// ===== UNIVERSE MANAGEMENT =====

var selectedUniverses = [];
var currentEditingUniverse = null;
var originalUniverseContent = null;

// Function to load and display universes
function loadUniverses() {
    console.log('loadUniverses called');
    var universeList = document.getElementById('universeList');
    
    if (!universeList) {
        console.error('universeList not found!');
        return;
    }
    
    try {
        var apiKey = ApiClient.accessToken();
        
        if (!apiKey) {
            universeList.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Not authenticated. Please log in first.</div>';
            return;
        }
        
        universeList.innerHTML = '<div class="fieldDescription">Loading universes...</div>';
        
        // Get all universes
        fetch('/Timeline/Universes', {
            headers: {
                'X-Emby-Token': apiKey
            }
        })
        .then(function(response) {
            console.log('Fetch universes response:', response.status);
            return response.json();
        })
        .then(function(universes) {
            console.log('Got universes:', universes);
            
            if (!universes || universes.length === 0) {
                universeList.innerHTML = '<div class="fieldDescription">No universes found. Create universe files in /config/universes/</div>';
                return;
            }
            
            // Display universes
            universeList.innerHTML = '';
            var container = document.createElement('div');
            container.style.display = 'grid';
            container.style.gap = '0.5em';
            
            universes.forEach(function(universe) {
                var card = document.createElement('div');
                card.className = 'paperList';
                card.style.padding = '1em';
                card.style.display = 'flex';
                card.style.justifyContent = 'space-between';
                card.style.alignItems = 'center';
                card.style.border = '1px solid #333';
                
                var leftSection = document.createElement('div');
                leftSection.style.display = 'flex';
                leftSection.style.alignItems = 'center';
                leftSection.style.gap = '1em';
                
                var checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'universe-checkbox';
                checkbox.dataset.filename = universe.filename;
                checkbox.checked = selectedUniverses.indexOf(universe.filename) !== -1;
                checkbox.onchange = function() {
                    if (this.checked) {
                        if (selectedUniverses.indexOf(universe.filename) === -1) {
                            selectedUniverses.push(universe.filename);
                        }
                    } else {
                        var index = selectedUniverses.indexOf(universe.filename);
                        if (index !== -1) {
                            selectedUniverses.splice(index, 1);
                        }
                    }
                    console.log('Selected universes:', selectedUniverses);
                };
                
                var info = document.createElement('div');
                
                var name = document.createElement('div');
                name.style.fontWeight = '500';
                name.textContent = universe.name;
                
                var details = document.createElement('div');
                details.className = 'fieldDescription';
                details.textContent = 'File: ' + universe.filename;
                
                info.appendChild(name);
                info.appendChild(details);
                
                leftSection.appendChild(checkbox);
                leftSection.appendChild(info);
                
                var editBtn = document.createElement('button');
                editBtn.type = 'button';
                editBtn.className = 'emby-button raised';
                editBtn.textContent = 'Edit';
                editBtn.onclick = function() {
                    openEditor(universe.filename);
                };
                
                card.appendChild(leftSection);
                card.appendChild(editBtn);
                container.appendChild(card);
            });
            
            universeList.appendChild(container);
            console.log('Universes displayed successfully');
        })
        .catch(function(error) {
            console.error('Error loading universes:', error);
            universeList.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Error loading universes: ' + error.message + '</div>';
        });
        
    } catch (error) {
        console.error('Exception in loadUniverses:', error);
        universeList.innerHTML = '<div class="fieldDescription" style="color: #ff6b6b;">Error loading universes: ' + error.message + '</div>';
    }
}

// Function to open JSON editor
function openEditor(filename) {
    console.log('Opening editor for:', filename);
    
    var apiKey = ApiClient.accessToken();
    
    fetch('/Timeline/Universes/' + filename, {
        headers: {
            'X-Emby-Token': apiKey
        }
    })
    .then(function(response) {
        return response.json();
    })
    .then(function(universe) {
        currentEditingUniverse = filename;
        originalUniverseContent = JSON.stringify(universe, null, 2);
        
        document.getElementById('editorUniverseName').textContent = universe.name;
        document.getElementById('jsonContent').value = originalUniverseContent;
        document.getElementById('jsonEditorSection').style.display = 'block';
        document.getElementById('validationErrors').style.display = 'none';
        
        // Scroll to editor
        document.getElementById('jsonEditorSection').scrollIntoView({ behavior: 'smooth' });
    })
    .catch(function(error) {
        alert('Error loading universe: ' + error.message);
    });
}

// Function to validate JSON
function validateJSON(jsonText) {
    try {
        var obj = JSON.parse(jsonText);
        
        var errors = [];
        
        if (!obj.key) {
            errors.push('Missing required field: key');
        }
        if (!obj.name) {
            errors.push('Missing required field: name');
        }
        if (!obj.items) {
            errors.push('Missing required field: items');
        } else if (!Array.isArray(obj.items)) {
            errors.push('Field "items" must be an array');
        }
        
        return { valid: errors.length === 0, errors: errors };
    } catch (e) {
        return { valid: false, errors: ['Invalid JSON syntax: ' + e.message] };
    }
}

// Function to save universe
function saveUniverse() {
    var jsonContent = document.getElementById('jsonContent').value;
    var validationErrors = document.getElementById('validationErrors');
    var saveBtn = document.getElementById('saveUniverseBtn');
    
    // Validate JSON
    var validation = validateJSON(jsonContent);
    
    if (!validation.valid) {
        validationErrors.style.display = 'block';
        validationErrors.innerHTML = '<strong>Validation Errors:</strong><br>' + validation.errors.join('<br>');
        return;
    }
    
    validationErrors.style.display = 'none';
    saveBtn.disabled = true;
    saveBtn.textContent = 'Saving...';
    
    var apiKey = ApiClient.accessToken();
    
    fetch('/Timeline/Universes/' + currentEditingUniverse, {
        method: 'POST',
        headers: {
            'X-Emby-Token': apiKey,
            'Content-Type': 'application/json'
        },
        body: jsonContent
    })
    .then(function(response) {
        return response.json();
    })
    .then(function(result) {
        saveBtn.disabled = false;
        saveBtn.textContent = 'Save Universe';
        
        if (result.success) {
            alert('Universe saved successfully!');
            cancelEdit();
            loadUniverses(); // Reload universe list
        } else {
            validationErrors.style.display = 'block';
            validationErrors.innerHTML = '<strong>Save Failed:</strong><br>' + result.errors.join('<br>');
        }
    })
    .catch(function(error) {
        saveBtn.disabled = false;
        saveBtn.textContent = 'Save Universe';
        alert('Error saving universe: ' + error.message);
    });
}

// Function to cancel editing
function cancelEdit() {
    document.getElementById('jsonEditorSection').style.display = 'none';
    document.getElementById('jsonContent').value = '';
    document.getElementById('validationErrors').style.display = 'none';
    currentEditingUniverse = null;
    originalUniverseContent = null;
}

// Function to select all universes
function selectAll() {
    var checkboxes = document.querySelectorAll('.universe-checkbox');
    selectedUniverses = [];
    checkboxes.forEach(function(checkbox) {
        checkbox.checked = true;
        selectedUniverses.push(checkbox.dataset.filename);
    });
    console.log('Selected all universes:', selectedUniverses);
}

// Function to deselect all universes
function deselectAll() {
    var checkboxes = document.querySelectorAll('.universe-checkbox');
    checkboxes.forEach(function(checkbox) {
        checkbox.checked = false;
    });
    selectedUniverses = [];
    console.log('Deselected all universes');
}

// Update createPlaylists function to use selected universes
function createPlaylistsForSelected() {
    console.log('Create playlists for selected universes');
    
    var createBtn = document.getElementById('createPlaylistsBtn');
    var statusMessage = document.getElementById('statusMessage');
    var outputBox = document.getElementById('outputBox');
    var errorBox = document.getElementById('errorBox');
    
    // Hide previous messages
    statusMessage.style.display = 'none';
    outputBox.style.display = 'none';
    errorBox.style.display = 'none';
    
    // Show loading state
    createBtn.disabled = true;
    createBtn.textContent = 'Creating...';
    
    statusMessage.style.display = 'block';
    statusMessage.style.background = '#1c4966';
    statusMessage.style.color = '#fff';
    statusMessage.textContent = 'Creating playlists for selected universes...';
    
    try {
        var apiKey = ApiClient.accessToken();
        var userId = ApiClient.getCurrentUserId();
        
        if (!apiKey || !userId) {
            throw new Error('Not authenticated. Please log in first.');
        }
        
        // Prepare request body with selected universes
        var requestBody = selectedUniverses.length > 0 ? {
            selectedUniverseFilenames: selectedUniverses
        } : null;
        
        console.log('Creating playlists with request:', requestBody);
        
        fetch('/Timeline/CreatePlaylists?userId=' + userId, {
            method: 'POST',
            headers: {
                'X-Emby-Token': apiKey,
                'Content-Type': 'application/json'
            },
            body: requestBody ? JSON.stringify(requestBody) : null
        })
        .then(function(response) {
            return response.json();
        })
        .then(function(data) {
            console.log('Create playlists result:', data);
            
            createBtn.disabled = false;
            createBtn.textContent = 'Create Playlists for Selected Universes';
            
            if (data.success) {
                statusMessage.style.background = '#1e5631';
                statusMessage.style.color = '#fff';
                statusMessage.textContent = data.message || 'Playlists created successfully!';
                
                setTimeout(function() {
                    loadPlaylists();
                }, 1000);
            } else {
                statusMessage.style.background = '#5a1a1a';
                statusMessage.style.color = '#ff6b6b';
                statusMessage.textContent = data.message || 'Failed to create playlists';
                
                if (data.errors && data.errors.length > 0) {
                    errorBox.style.display = 'block';
                    errorBox.textContent = data.errors.join('\n');
                }
            }
        })
        .catch(function(error) {
            console.error('Error creating playlists:', error);
            
            createBtn.disabled = false;
            createBtn.textContent = 'Create Playlists for Selected Universes';
            
            statusMessage.style.display = 'block';
            statusMessage.style.background = '#5a1a1a';
            statusMessage.style.color = '#ff6b6b';
            statusMessage.textContent = 'Error creating playlists';
            
            errorBox.style.display = 'block';
            errorBox.textContent = error.message;
        });
        
    } catch (error) {
        console.error('Exception in createPlaylistsForSelected:', error);
        
        createBtn.disabled = false;
        createBtn.textContent = 'Create Playlists for Selected Universes';
        
        statusMessage.style.display = 'block';
        statusMessage.style.background = '#5a1a1a';
        statusMessage.style.color = '#ff6b6b';
        statusMessage.textContent = 'Error: ' + error.message;
    }
}

// Initialize universe management on page load
setTimeout(function() {
    loadUniverses();
    
    // Select All button
    var selectAllBtn = document.getElementById('selectAllBtn');
    if (selectAllBtn) {
        selectAllBtn.addEventListener('click', selectAll);
    }
    
    // Deselect All button
    var deselectAllBtn = document.getElementById('deselectAllBtn');
    if (deselectAllBtn) {
        deselectAllBtn.addEventListener('click', deselectAll);
    }
    
    // Refresh Universes button
    var refreshUniversesBtn = document.getElementById('refreshUniversesBtn');
    if (refreshUniversesBtn) {
        refreshUniversesBtn.addEventListener('click', loadUniverses);
    }
    
    // Save Universe button
    var saveUniverseBtn = document.getElementById('saveUniverseBtn');
    if (saveUniverseBtn) {
        saveUniverseBtn.addEventListener('click', saveUniverse);
    }
    
    // Cancel Edit button
    var cancelEditBtn = document.getElementById('cancelEditBtn');
    if (cancelEditBtn) {
        cancelEditBtn.addEventListener('click', cancelEdit);
    }
    
    // Update Create Playlists button to use selected universes
    var createBtn = document.getElementById('createPlaylistsBtn');
    if (createBtn) {
        // Remove old event listener by replacing the button
        var newCreateBtn = createBtn.cloneNode(true);
        createBtn.parentNode.replaceChild(newCreateBtn, createBtn);
        
        newCreateBtn.addEventListener('click', createPlaylistsForSelected);
    }
}, 500);
