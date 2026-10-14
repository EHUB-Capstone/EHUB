import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

// Custom type-scale tokens from src/index.css (--text-<name>). Without this,
// tailwind-merge reads `text-display` as a text color and drops it whenever a
// color such as `text-white` or `text-secondary` is merged in the same call.
const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      "font-size": [
        { text: ["display", "heading", "subheading", "body-lg", "body", "body-sm", "caption", "overline"] },
      ],
    },
  },
});

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
