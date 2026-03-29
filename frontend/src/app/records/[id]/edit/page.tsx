"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, put } from "@/lib/api";
import type { RecordEntry } from "@/types/models";
import { SENSOR_CATALOG } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";

export default function EditRecordPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [subjectType, setSubjectType] = useState("");
  const [modelUrl, setModelUrl] = useState("");
  const [sensorsUsed, setSensorsUsed] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    get<RecordEntry>(`/records/${id}`)
      .then((data) => {
        setSubjectType(data.subject.type);
        setModelUrl(data.subject["3d_model_url"] || "");
        setSensorsUsed(data.sensors_used);
      })
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load record"),
      )
      .finally(() => setLoading(false));
  }, [id]);

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
    setSaving(true);
    setError(null);

    try {
      await put<RecordEntry>(`/records/${id}`, {
        subject: {
          type: subjectType,
          "3d_model_url": modelUrl,
        },
        sensors_used: sensorsUsed,
      });
      router.push(`/records/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update record");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <h1>Edit Record</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Subject Type"
            name="subject_type"
            type="text"
            value={subjectType}
            onChange={(e) => setSubjectType((e.target as HTMLInputElement).value)}
            required
          />

          <FormField
            label="3D Model URL"
            name="model_url"
            type="url"
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
            <Button type="submit" loading={saving}>
              Save Changes
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push(`/records/${id}`)}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
