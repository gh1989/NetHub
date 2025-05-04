import { useState, useEffect } from 'react'
import './App.css'

function App() {
  const [jobs, setJobs] = useState([]);
  const [jobType, setJobType] = useState('Simulation');
  const [duration, setDuration] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const apiUrl = 'http://localhost:5155/api';
  
  useEffect(() => {
    // Fetch jobs when component mounts
    fetchJobs();
    
    // Set up polling to refresh jobs every 5 seconds
    const interval = setInterval(fetchJobs, 5000);
    
    // Clean up interval on unmount
    return () => clearInterval(interval);
  }, []);
  
  const fetchJobs = async () => {
    try {
      const response = await fetch(`${apiUrl}/jobs`);
      if (!response.ok) {
        throw new Error('Failed to fetch jobs');
      }
      const data = await response.json();
      setJobs(data);
      setLoading(false);
    } catch (err) {
      console.error('Error fetching jobs:', err);
      setError('Failed to load jobs. Please try again.');
      setLoading(false);
    }
  };
  
  const handleSubmit = async (e) => {
    e.preventDefault();
    
    try {
      const response = await fetch(`${apiUrl}/jobs`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          jobType,
          durationSeconds: Number(duration)
        }),
      });
      
      if (!response.ok) {
        throw new Error('Failed to create job');
      }
      
      // Refresh the job list
      fetchJobs();
      
      // Reset form
      setJobType('Simulation');
      setDuration(10);
    } catch (err) {
      console.error('Error creating job:', err);
      setError('Failed to submit job. Please try again.');
    }
  };
  
  // Helper function to get status color
  const getStatusColor = (status) => {
    switch (status) {
      case 'Queued': return '#3498db'; // Blue
      case 'Running': return '#f39c12'; // Orange
      case 'Completed': return '#2ecc71'; // Green
      case 'Failed': return '#e74c3c'; // Red
      default: return '#95a5a6'; // Gray
    }
  };

  return (
    <div className="container">
      <h1>netHUB</h1>
      
      <div className="job-form-container">
        <h2>Submit a New Job</h2>
        {error && <div className="error-message">{error}</div>}
        
        <form onSubmit={handleSubmit} className="job-form">
          <div className="form-group">
            <label htmlFor="jobType">Job Type:</label>
            <select 
              id="jobType" 
              value={jobType} 
              onChange={(e) => setJobType(e.target.value)}
            >
              <option value="Evaluation">Evaluation</option>
              <option value="Training">Training</option>
            </select>
          </div>
          
          <div className="form-group">
            <label htmlFor="duration">Duration (seconds):</label>
            <input 
              type="number" 
              id="duration" 
              value={duration} 
              onChange={(e) => setDuration(e.target.value)}
              min="1"
              max="300"
            />
          </div>
          
          <button type="submit" className="submit-button">Submit Job</button>
        </form>
      </div>
      
      <div className="jobs-list-container">
        <h2>Jobs</h2>
        {loading ? (
          <p>Loading jobs...</p>
        ) : jobs.length === 0 ? (
          <p>No jobs found. Submit a new job to get started.</p>
        ) : (
          <table className="jobs-table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Type</th>
                <th>Status</th>
                <th>Created</th>
                <th>Duration</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map(job => (
                <tr key={job.id}>
                  <td>{job.id.substring(0, 8)}...</td>
                  <td>{job.jobType}</td>
                  <td>
                    <span 
                      className="status-badge"
                      style={{ backgroundColor: getStatusColor(job.status) }}
                    >
                      {job.status}
                    </span>
                  </td>
                  <td>{new Date(job.createdAt).toLocaleString()}</td>
                  <td>{job.durationSeconds}s</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

export default App
