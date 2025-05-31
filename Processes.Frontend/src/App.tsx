import { useEffect, useState } from "react";
import { BrowserRouter as Router, Routes, Route, Link } from "react-router-dom";
import { fetchProcesses, createProcess } from "./api";
import type { Process } from "./api";
import "./App.css";
import ProcessForm from "./components/ProcessForm";
import ProcessTable from "./components/ProcessTable";
import ProcessDetailView from "./components/ProcessDetailView"; // New import

/**
 * Main App component for Processes.Frontend
 * - Lists processes from the API
 * - Allows creating a new process
 */
function App() {
  const [processes, setProcesses] = useState<Process[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState<boolean>(false);

  // Function to refresh processes
  const refreshProcesses = async () => {
    setLoading(true);
    setError(null);
    try {
      const fetchedProcesses = await fetchProcesses();
      setProcesses(fetchedProcesses);
    } catch (e) {
      if (e instanceof Error) {
        setError(e.message);
      } else {
        setError("Unknown error");
      }
    } finally {
      setLoading(false);
    }
  };

  // Fetch processes on mount
  useEffect(() => {
    refreshProcesses();
  }, []);

  /**
   * Handle form submission to create a new process
   */
  async function handleCreate(name: string, subCount: number, type: number) {
    setCreating(true);
    setError(null);
    try {
      await createProcess(name, subCount, type);
      await refreshProcesses(); // Refresh the list after creation
    } catch (e) {
      if (e instanceof Error) {
        setError(e.message);
      } else {
        setError("Unknown error");
      }
    } finally {
      setCreating(false);
    }
  }

  return (
    <Router>
      <div className="app-container">
        <h1 className="app-title">
          <Link to="/" className="app-title-link">
            Processes
          </Link>
        </h1>
        <Routes>
          <Route
            path="/"
            element={
              <>
                <ProcessForm onCreate={handleCreate} creating={creating} />
                <ProcessTable
                  processes={processes}
                  loading={loading}
                  error={error}
                  onProcessAction={refreshProcesses} // Pass refresh function
                />
              </>
            }
          />
          <Route
            path="/processes/:id"
            element={<ProcessDetailView onProcessAction={refreshProcesses} />}
          />
        </Routes>
      </div>
    </Router>
  );
}

export default App;
