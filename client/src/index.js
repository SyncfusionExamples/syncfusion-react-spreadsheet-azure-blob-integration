import { createRoot } from 'react-dom/client';
import './index.css';
import * as React from 'react';
import { useState } from 'react';
import { DropDownListComponent } from '@syncfusion/ej2-react-dropdowns';
import { SpreadsheetComponent } from '@syncfusion/ej2-react-spreadsheet';

/**
 * Default Spreadsheet sample
 */
function Default() {
  let spreadsheet;
  const fileList = [
    { name: 'Car Sales Report', extension: '.xlsx' },
    { name: 'Shopping Cart', extension: '.xls' },
    { name: 'Price Details', extension: '.csv' },
  ];

  const fields = { text: 'name' };

  const [fileInfo, setFileInfo] = useState(fileList[0]);
  const [loadedFileInfo, setLoadedFileInfo] = useState(null);
  // Function to open a spreadsheet file from Azure blob via an API call
  const openFromAzure = () => {
    spreadsheet.showSpinner();
    // Make a POST request to the backend API to fetch the file from Azure blob
    fetch('https://localhost:44341/api/spreadsheet/OpenFromAzure', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        FileName: fileInfo.name, // Name of the file to open
        Extension: fileInfo.extension, // File extension
      }),
    })
      .then((response) => response.json()) // Parse the response as JSON
      .then((data) => {
        spreadsheet.hideSpinner();
        // Load the spreadsheet data into the UI
        spreadsheet.openFromJson({ file: data, triggerEvent: true });
      })
      .catch((error) => {
        window.alert('Error importing file:', error);
      });
  };

  // Function to save the current spreadsheet to Azure blob via an API call
  const saveToAzure = () => {
    // Convert the current spreadsheet to JSON format
    spreadsheet.saveAsJson().then((json) => {
      const formData = new FormData();

      // Append necessary data to the form for the API request
      formData.append('FileName', loadedFileInfo.fileName); // Name of the file to save
      formData.append('saveType', loadedFileInfo.saveType); // Save type
      formData.append('JSONData', JSON.stringify(json.jsonObject.Workbook)); // Spreadsheet data
      formData.append(
        'PdfLayoutSettings',
        JSON.stringify({ FitSheetOnOnePage: false }) // PDF layout settings
      );

      // Make a POST request to the backend API to save the file to Azure Blob Storage
      fetch('https://localhost:44341/api/spreadsheet/SaveToAzure', {
        method: 'POST',
        body: formData,
      })
        .then((response) => {
          if (!response.ok) {
            throw new Error(
              `Save request failed with status ${response.status}`
            );
          }
          window.alert('Workbook saved successfully to Azure Blob Storage.');
        })
        .catch((error) => {
          window.alert('Error saving to server:', error);
        });
    });
  };

  const onCreated = () => {};

  const beforeSave = (args) => {
    args.isFullPost = false;
    console.log(args);
  };

  const openComplete = (args) => {
    if (args.response.isOpenFromJson) {
      const saveTypes = { '.xlsx': 'Xlsx', '.xls': 'Xls', '.csv': 'Csv' };
      setLoadedFileInfo({
        fileName: fileInfo.name,
        saveType: saveTypes[fileInfo.extension],
      });
    }
  };

  const fileChangeHandler = (args) => {
    setFileInfo(args.itemData);
  };

  return (
    <div className="control-pane">
      <div className="control-section spreadsheet-control">
        <label for="filename-ddl">Select a file:</label>
        <DropDownListComponent
          id="filename-ddl"
          dataSource={fileList}
          fields={fields}
          index={0}
          width={175}
          change={fileChangeHandler}
        />
        <button
          className="e-btn"
          onClick={openFromAzure}
          style={{ marginLeft: '10px' }}
        >
          Open from Azure
        </button>
        <button
          className="e-btn"
          onClick={saveToAzure}
          style={{ marginLeft: '10px' }}
          disabled={loadedFileInfo == null}
        >
          Save to Azure
        </button>
        <SpreadsheetComponent
          openUrl="https://localhost:44341/api/spreadsheet/Open"
          saveUrl="https://localhost:44341/api/spreadsheet/Save"
          ref={(ssObj) => {
            spreadsheet = ssObj;
          }}
          created={onCreated}
          beforeSave={beforeSave}
          openComplete={openComplete}
        ></SpreadsheetComponent>
      </div>
    </div>
  );
}
export default Default;

const root = createRoot(document.getElementById('sample'));
root.render(<Default />);
