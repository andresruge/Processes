import { useEffect, useState } from "react";
import { fetchProcesses, createProcess } from "./api";
import type { Process } from "./api";
import "./App.css";
import ProcessForm from "./components/ProcessForm";
import ProcessTable from "./components/ProcessTable";

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

  // Fetch processes on mount
  useEffect(() => {
    setLoading(true);
    fetchProcesses()
      .then(setProcesses)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  /**
   * Handle form submission to create a new process
   */
  async function handleCreate(name: string, subCount: number, type: number) {
    setCreating(true);
    setError(null);
    try {
      const created = await createProcess(name, subCount, type);
      setProcesses([created, ...processes]);
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
    <div className="app-container">
      <h1 className="app-title">Processes</h1>
      <ProcessForm onCreate={handleCreate} creating={creating} />
      <ProcessTable processes={processes} loading={loading} error={error} />
    </div>
  );
}

export default App;
