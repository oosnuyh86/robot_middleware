export const SENSOR_CATALOG = [
  "Intel RealSense D435i",
  "HTC Vive Tracker",
  "Alicat Flow Meter",
] as const;

export type SensorName = (typeof SENSOR_CATALOG)[number];

export function isValidSensor(name: string): name is SensorName {
  return SENSOR_CATALOG.includes(name as SensorName);
}

export function validateSensors(sensors: string[]): string[] {
  const invalid = sensors.filter((s) => !isValidSensor(s));
  if (invalid.length > 0) {
    throw new Error(`Invalid sensors: ${invalid.join(", ")}. Valid sensors: ${SENSOR_CATALOG.join(", ")}`);
  }
  return sensors;
}
