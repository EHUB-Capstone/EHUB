import { createContext, useContext, useState, useEffect } from 'react';
import type { ReactNode } from 'react';

interface ThemeContextType {
  isDark: boolean;
  toggleTheme: () => void;
}

// The legacy 'ehub-theme' key was written automatically (default dark) for every
// visitor, so it cannot tell us what a user chose. Only an explicit toggle is stored.
const THEME_PREF_KEY = 'ehub-theme-pref';

const ThemeContext = createContext<ThemeContextType>({
  isDark: false,
  toggleTheme: () => {},
});

const readPreference = (): boolean => {
  try {
    return localStorage.getItem(THEME_PREF_KEY) === 'dark';
  } catch {
    return false;
  }
};

export const ThemeProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [isDark, setIsDark] = useState<boolean>(readPreference);

  useEffect(() => {
    document.documentElement.style.colorScheme = isDark ? 'dark' : 'light';
    document.documentElement.classList.toggle('dark', isDark);
  }, [isDark]);

  const toggleTheme = () => {
    setIsDark((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(THEME_PREF_KEY, next ? 'dark' : 'light');
      } catch {
        // Storage can be blocked; the theme still changes for this session.
      }
      return next;
    });
  };

  return (
    <ThemeContext.Provider value={{ isDark, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => useContext(ThemeContext);
