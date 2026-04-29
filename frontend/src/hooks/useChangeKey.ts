import { useEffect, useRef, useState } from "react";

interface Options {
  onlyIncrease?: boolean;
}

export function useChangeKey(value: number, options: Options = {}): number {
  const [key, setKey] = useState(0);
  const prev = useRef<number | undefined>(undefined);
  const onlyIncrease = options.onlyIncrease ?? false;

  useEffect(() => {
    if (prev.current !== undefined) {
      const changed = onlyIncrease ? value > prev.current : value !== prev.current;
      if (changed) setKey((k) => k + 1);
    }
    prev.current = value;
  }, [value, onlyIncrease]);

  return key;
}
