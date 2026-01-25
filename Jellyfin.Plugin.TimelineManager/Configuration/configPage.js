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
