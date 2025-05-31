import { useState } from "react";
import type { FormEvent, ChangeEvent } from "react";
import "./ProcessForm.css";

const PROCESS_TYPES = [
  { label: "ProcessTypeA", value: 0 },
  { label: "ProcessTypeB", value: 1 },
];

interface ProcessFormProps {
  onCreate: (name: string, subCount: number, type: number) => Promise<void>;
  creating: boolean;
}

function ProcessForm({ onCreate, creating }: ProcessFormProps) {
  const [newName, setNewName] = useState<string>("");
  const [newSubCount, setNewSubCount] = useState<number>(1);
  const [newType, setNewType] = useState<number>(0);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim() || newSubCount < 1) return;
    await onCreate(newName.trim(), newSubCount, newType);
    setNewName("");
    setNewSubCount(1);
    setNewType(0);
  }

  return (
    <form onSubmit={handleSubmit} className="process-form">
      <input
        type="text"
        value={newName}
        onChange={(e) => setNewName(e.target.value)}
        placeholder="New process name"
        disabled={creating}
        className="process-input name"
      />
      <input
        type="number"
        min={1}
        value={newSubCount}
        onChange={(e: ChangeEvent<HTMLInputElement>) =>
          setNewSubCount(Number(e.target.value))
        }
        placeholder="Subprocesses"
        disabled={creating}
        className="process-input subcount"
      />
      <select
        value={newType}
        onChange={(e) => setNewType(Number(e.target.value))}
        disabled={creating}
        className="process-select"
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
        className="process-button"
      >
        {creating ? "Creating..." : "Create"}
      </button>
    </form>
  );
}

export default ProcessForm;
