import { useEffect, useState } from "react";
import type { FormEvent, ChangeEvent } from "react";
import { fetchProcesses, createProcess } from "./api";
import type { Process } from "./api";
import "./App.css";

// Enum for process types (should match backend)
const PROCESS_TYPES = [
  { label: "ProcessTypeA", value: 0 },
  { label: "ProcessTypeB", value: 1 },
];

/**
 * Main App component for Processes.Frontend
 * - Lists processes from the API
 * - Allows creating a new process
 */
function App() {
  const [processes, setProcesses] = useState<Process[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [newName, setNewName] = useState<string>("");
  const [newSubCount, setNewSubCount] = useState<number>(1);
  const [newType, setNewType] = useState<number>(0);
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
  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim() || newSubCount < 1) return;
    setCreating(true);
    setError(null);
    try {
      const created = await createProcess(newName.trim(), newSubCount, newType);
      setProcesses([created, ...processes]);
      setNewName("");
      setNewSubCount(1);
      setNewType(0);
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
    <div
      style={{ maxWidth: 700, margin: "2rem auto", fontFamily: "sans-serif" }}
    >
      <h1 style={{ letterSpacing: 1, fontWeight: 700, fontSize: 32, color: '#2d3748', marginBottom: 24 }}>Processes</h1>
      <form onSubmit={handleCreate} style={{ marginBottom: 32, display: 'flex', gap: 8, alignItems: 'center' }}>
        <input
          type="text"
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          placeholder="New process name"
          disabled={creating}
          style={{ padding: 10, width: 180, borderRadius: 4, border: '1px solid #ccc' }}
        />
        <input
          type="number"
          min={1}
          value={newSubCount}
          onChange={(e: ChangeEvent<HTMLInputElement>) => setNewSubCount(Number(e.target.value))}
          placeholder="Subprocesses"
          disabled={creating}
          style={{ padding: 10, width: 60, borderRadius: 4, border: '1px solid #ccc' }}
        />
        <select
          value={newType}
          onChange={(e) => setNewType(Number(e.target.value))}
          disabled={creating}
          style={{ padding: 10, borderRadius: 4, border: '1px solid #ccc' }}
        >
          {PROCESS_TYPES.map((pt) => (
            <option key={pt.value} value={pt.value}>
              {pt.label}
            </option>
          ))}
        </select>
        <button
          type="submit"
          disabled={creating || !newName.trim() || newSubCount < 1}
          style={{ padding: '10px 18px', borderRadius: 4, background: '#3182ce', color: '#fff', border: 'none', fontWeight: 600, cursor: creating ? 'not-allowed' : 'pointer' }}
        >
          {creating ? "Creating..." : "Create"}
        </button>
      </form>
      {error && <div style={{ color: "#e53e3e", marginBottom: 16 }}>{error}</div>}
      {loading ? (
        <div>Loading...</div>
      ) : (
        <table style={{ width: "100%", borderCollapse: "separate", borderSpacing: 0, background: '#fff', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.07)' }}>
          <thead style={{ background: '#f7fafc' }}>
            <tr>
              <th style={{ textAlign: "left", borderBottom: "2px solid #e2e8f0", padding: '12px 16px', fontWeight: 700, color: '#4a5568' }}>
                Name
              </th>
              <th style={{ textAlign: "left", borderBottom: "2px solid #e2e8f0", padding: '12px 16px', fontWeight: 700, color: '#4a5568' }}>
                Status
              </th>
              <th style={{ textAlign: "left", borderBottom: "2px solid #e2e8f0", padding: '12px 16px', fontWeight: 700, color: '#4a5568' }}>
                Created
              </th>
            </tr>
          </thead>
          <tbody>
            {processes.length === 0 ? (
              <tr>
                <td colSpan={3} style={{ padding: 20, textAlign: 'center', color: '#718096' }}>No processes found.</td>
              </tr>
            ) : (
              processes.map((proc, idx) => (
                <tr key={proc.id} style={{ background: idx % 2 === 0 ? '#f7fafc' : '#fff' }}>
                  <td style={{ padding: '12px 16px', borderBottom: '1px solid #e2e8f0', fontWeight: 500 }}>{proc.name}</td>
                  <td style={{ padding: '12px 16px', borderBottom: '1px solid #e2e8f0', color: '#2b6cb0', fontWeight: 500 }}>{proc.status}</td>
                  <td style={{ padding: '12px 16px', borderBottom: '1px solid #e2e8f0', color: '#718096' }}>{new Date(proc.createdAt).toLocaleString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}

export default App;
