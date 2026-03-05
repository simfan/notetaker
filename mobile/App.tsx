import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import NoteListScreen from './src/screens/NoteListScreen';
import NoteEditorScreen from './src/screens/NoteEditorScreen';
import TagManagerScreen from './src/screens/TagManagerScreen';
import { RootStackParamList } from './src/types';

const Stack = createNativeStackNavigator<RootStackParamList>();

export default function App() {
  return (
    <SafeAreaProvider>
      <NavigationContainer>
        <Stack.Navigator id={undefined} screenOptions={{ headerShown: false }}>
          <Stack.Screen name="NoteList" component={NoteListScreen} />
          <Stack.Screen name="NoteEditor" component={NoteEditorScreen} />
          <Stack.Screen name="TagManager" component={TagManagerScreen} />
        </Stack.Navigator>
      </NavigationContainer>
    </SafeAreaProvider>
  );
}