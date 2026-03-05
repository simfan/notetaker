import { createClient } from '@supabase/supabase-js';
import AsyncStorage from '@react-native-async-storage/async-storage';

// Replace with your Supabase project URL and anon key
//const SUPABASE_URL = 'https://flfpavpvtayuwxhnauto.supabase.co';
//const SUPABASE_ANON_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZsZnBhdnB2dGF5dXd4aG5hdXRvIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzI2Mjg2NTUsImV4cCI6MjA4ODIwNDY1NX0.RdJJTX4O83GRJZspaH7LuEPvJ-Ow9x7wyCSF3VRdw2o';
const SUPABASE_URL = process.env.EXPO_PUBLIC_SUPABASE_URL!;
const SUPABASE_ANON_KEY = process.env.EXPO_PUBLIC_SUPABASE_ANON_KEY!;

export const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
  auth: {
    storage: AsyncStorage,
    autoRefreshToken: true,
    persistSession: true,
    detectSessionInUrl: false,
  },
});
