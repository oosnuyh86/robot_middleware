"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { post, get } from "@/lib/api";
import type { Professional, RecordEntry } from "@/types/models";
import { SENSOR_CATALOG } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";

export default function NewRecordPage() {
  const router = useRouter();
  const [professionalId, setProfessionalId] = useState("");
  const [subjectType, setSubjectType] = useState("");
  const [modelUrl, setModelUrl] = useState("");
  const [sensorsUsed, setSensorsUsed] = useState<string[]>([]);
  const [professionals, setProfessionals] = useState<Professional[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    get<Professional[]>("/professionals")
      .then(setProfessionals)
      .catch(() => {});
  }, []);

  function handleSensorToggle(sensor: string) {
    setSensorsUsed((prev) =>
      prev.includes(sensor)
        ? prev.filter((s) => s !== sensor)
        : [...prev, sensor],
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (sensorsUsed.length === 0) {
      setError("Select at least one sensor");
      return;
    }
    setLoading(true);
    setError(null);

    try {
      const record = await post<RecordEntry>("/records", {
        professional_id: professionalId,
        subject: {
          type: subjectType,
          "3d_model_url": modelUrl,
        },
        sensors_used: sensorsUsed,
      });
      router.push(`/records/${record._id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create record");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-container">
      <h1>Create Record</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <Select
            label="Professional"
            name="professional_id"
            value={professionalId}
            onChange={(e) => setProfessionalId(e.target.value)}
            options={professionals.map((p) => ({
              value: p._id,
              label: p.name,
            }))}
            required
          />

          <FormField
            label="Subject Type"
            name="subject_type"
            type="text"
            placeholder="e.g., composite-part, aircraft-wing"
            value={subjectType}
            onChange={(e) => setSubjectType((e.target as HTMLInputElement).value)}
            required
          />

          <FormField
            label="3D Model URL"
            name="model_url"
            type="url"
            placeholder="S3 URL will be assigned after upload"
            value={modelUrl}
            onChange={(e) => setModelUrl((e.target as HTMLInputElement).value)}
          />

          <div className="form-field">
            <label>
              Sensors Used <span className="required">*</span>
            </label>
            <div className="checkbox-group">
              {SENSOR_CATALOG.map((sensor) => (
                <label key={sensor}>
                  <input
                    type="checkbox"
                    checked={sensorsUsed.includes(sensor)}
                    onChange={() => handleSensorToggle(sensor)}
                  />
                  {sensor}
                </label>
              ))}
            </div>
          </div>

          <div className="form-actions">
            <Button type="submit" loading={loading}>
              Create Record
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push("/records")}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
