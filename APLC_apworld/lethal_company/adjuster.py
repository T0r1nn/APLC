from kivy.app import App
from kivy.metrics import dp
#from kivy.uix.widget import Widget
from kivy.properties import NumericProperty, ReferenceListProperty, ObjectProperty
from kivy.uix.boxlayout import BoxLayout
from kivy.uix.label import Label
from kivy.uix.textinput import TextInput
from kivy.lang import Builder
#from kivy.properties import ColorProperty
#from kivy.graphics import Color
from kivy.uix.button import Button
from kivy.uix.popup import Popup
from kivy.uix.scrollview import ScrollView

#from json import loads as is_valid_json
from os import path

# this solution has a massive performance inpact when dealing with large input
'''Builder.load_string("""

<ScrollableTextInput>
    logic_input: logicinput
                    
    size_hint: (1,0.6)
    do_scroll_x: False
    do_scroll_y: True
    TextInput:
        id: logicinput
        multiline: True
        size_hint: (1,None)
        height: max(self.minimum_height, logic_scroller.height)

""")'''

class ScrollableTextInput(ScrollView):
    logic_input = ObjectProperty(None)

class LCContentImporterApp(App):
    
    def on_submit(self, instance):
        
        if not self.name_input.text or path.exists(f'{path.abspath(".")}\\custom_worlds\\lethal_company[{self.name_input.text}].apworld'):
            popup_layout = BoxLayout(orientation='vertical')
            status_label = Label(text='Error: apworld detected with the same or default name. Would you like to replace it?', halign='center', valign='top', text_size=(200, None))
            confirm_layout = BoxLayout(orientation='horizontal')
            yes_button = Button(text='Yes', halign='right', valign='center')
            no_button = Button(text='No', halign='left', valign='center')
            
            confirm_layout.add_widget(yes_button)
            confirm_layout.add_widget(no_button)
            popup_layout.add_widget(status_label)
            popup_layout.add_widget(confirm_layout)

            finished_popup = Popup(title='Import Status', content=popup_layout, size_hint=(None, None), size=(dp(300), dp(200)))
            yes_button.bind(on_press=lambda func:self.create_apworld(replace_world=True))      # type:ignore
            yes_button.bind(on_press=finished_popup.dismiss)                        # type:ignore
            no_button.bind(on_press=finished_popup.dismiss)                        # type:ignore
            finished_popup.open()
        else :
            self.create_apworld(replace_world=False)


    def create_apworld(self, replace_world = False):
        
        completion_status = True
        status_message = "Content successfully imported!"
        player_name = self.name_input.text

        #try:
        #    completion_status = is_valid_json(self.logic_input.text)
        #except ValueError:
        #    completion_status = False
        #    status_message = "Error: The given logic string is not valid json"
        

        if completion_status:
            import zipfile
            from os import rename, remove

            zip_file_path = f'{path.abspath(".")}\\custom_worlds\\lethal_company.apworld'      # change this to the current folder
            extract_to_path = f'{path.abspath(".")}\\custom_worlds\\'                          # change this to the custom_worlds folder relative to the client

            success = True

            compression_format = 0

            with zipfile.ZipFile(zip_file_path, 'r') as zip_ref:
                info = zip_ref.infolist()
                #for fileinfo in info:
                compression_format = info[0].compress_type
            

            try:
                with zipfile.ZipFile(zip_file_path, 'r') as zip_ref:
                    zip_ref.extractall(extract_to_path)
                print(f"Successfully extracted '{zip_file_path}' to '{extract_to_path}'")

            except zipfile.BadZipFile:
                success = False
                status_message = f"Error: '{zip_file_path}' is not a valid ZIP file."
            except FileNotFoundError:
                success = False
                status_message = f"Error: '{zip_file_path}' not found."
            except PermissionError:
                success = False
                status_message = f"Error: Cannot access '{extract_to_path}'. Permission denied."
            except Exception as e:
                success = False
                status_message = f"An error occurred: {e}"

            extracted_folder_path = extract_to_path + 'lethal_company\\'

            if success:
            
                if player_name: # before zipping, change folder name to lethal_company-['player_name']
                    extracted_folder_path = extract_to_path + f'lethal_company-[{player_name}]\\'
                    rename(extract_to_path + 'lethal_company\\', extracted_folder_path)

                try:
                    with open(extracted_folder_path + 'imported.py', 'w') as f:
                        f.write(f'data = {self.logic_input.text}')
                    if player_name:
                        with open(extracted_folder_path + 'custom_content.py', 'w') as f:
                            f.write(f'custom_content = {{\n\t"name": " - [{player_name}]"\n}}')
                except FileNotFoundError:
                    success = False
                    status_message = f"Error: path {extracted_folder_path + 'imported.py'} not found"    # need to handle access violations as well

            if success:
                import pathlib
                directory = pathlib.Path(extracted_folder_path)
                with zipfile.ZipFile(extract_to_path + "lethal_company.zip", mode="w", compression=compression_format) as archive:
                    for file_path in directory.rglob("*"):
                        if not player_name or file_path.name != 'adjuster.py':
                            archive.write(file_path, arcname=file_path.relative_to(extract_to_path))

                # once zipped back up, change file name to lethal_company['player_name'].apworld
                apworld_path = extract_to_path + "lethal_company.apworld" if not player_name else extract_to_path + f'lethal_company[{player_name}].apworld'
                if replace_world:
                    remove(apworld_path)        # this will remove the old apworld if the player agreed to
                try:
                    rename(extract_to_path + "lethal_company.zip", apworld_path)
                except FileExistsError:
                    success = False
                    status_message = f"Error: file {apworld_path} already exists"
                    remove(extract_to_path + "lethal_company.zip")


            # clean up by deleting the temporary folder at extracted_folder_path
            from shutil import rmtree
            if path.exists(extracted_folder_path):
                rmtree(extracted_folder_path)

        popup_layout = BoxLayout(orientation='vertical')
        status_label = Label(text=status_message, halign='center', valign='top', text_size=(200, None))
        close_label = Label(text='Click anywhere to close', halign='center', valign='center')
        popup_layout.add_widget(status_label)
        popup_layout.add_widget(close_label)

        finished_popup = Popup(title='Import Status', content=popup_layout, size_hint=(None, None), size=(dp(300), dp(200)))
        finished_popup.open()
    
    def build(self):

        root = BoxLayout(orientation='vertical')

        name_layout = BoxLayout(orientation='horizontal', size_hint_y=None, height=dp(34), padding=(0,2,0,2))
        name_box = Label(text='Game Name:', size=(dp(150),dp(30)), size_hint=(None,None), valign='center')
        self.name_input = TextInput(multiline=False, height=dp(30), size_hint_y=None)
        name_layout.add_widget(name_box)
        name_layout.add_widget(self.name_input)


        # this is part of the scroll bar solution
        #logic_scroller = ScrollView(size_hint=(1,0.6), do_scroll_x = False, do_scroll_y = True)
        #self.logic_input = TextInput(multiline=True,size_hint=(1,None), height=max(root.minimum_height, logic_scroller.height))
        #self.logic_input = ScrollableTextInput()
        #logic_scroller.add_widget(self.logic_input)
        self.logic_input = TextInput(multiline=True,size_hint=(1,0.6))

        submit_button = Button(text='Generate APWorld', size_hint=(1, None))
        submit_button.bind(on_press=self.on_submit)      # type:ignore

        root.add_widget(name_layout)
        root.add_widget(self.logic_input)
        root.add_widget(submit_button)
        
        return root

def launch():
    LCContentImporterApp().run()

if __name__ == '__main__':
    LCContentImporterApp().run()